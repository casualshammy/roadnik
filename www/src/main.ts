import * as L from "leaflet"
import * as Api from "./modules/api";
import * as Maps from "./modules/maps"
import { TimeSpan } from "./modules/timespan";
import { JsToCSharpMsg, MapViewState } from "./modules/api";
import { groupBy } from "./modules/toolkit";

const p_storageApi = new Api.StorageApi();

const queryString = window.location.search;
const urlParams = new URLSearchParams(queryString);
const p_roomId = urlParams.get('id');

const p_lastAlts = new Map<string, number>();
const p_markers = new Map<string, L.Marker>();
const p_circles = new Map<string, L.Circle>();
const p_paths = new Map<string, L.Polyline>();

let p_firstDataReceived = false;
let p_lastOffset = 0;
let p_currentLayer: string | undefined = undefined;
let p_userColorIndex = 0;

const p_mapsData = Maps.GetMapLayers();
const p_overlays = Maps.GetMapOverlayLayers();

const p_map = new L.Map('map', {
    center: new L.LatLng(51.4768, 0.0006),
    zoom: 14,
    layers: [p_mapsData.array[0].tileLayer]
});
p_map.attributionControl.setPrefix(false);
p_map.on('baselayerchange', function (_e) {
    p_currentLayer = _e.name;
    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED, data: p_currentLayer });
});
p_map.on('zoomend', function (_e) {
    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED, data: getMapViewState() });
});
p_map.on('moveend', function (_e) {
    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED, data: getMapViewState() });
});
p_currentLayer = p_mapsData.array[0].name;

const p_layersControl = new L.Control.Layers(p_mapsData.obj, p_overlays);
p_map.addControl(p_layersControl);

async function updateViewAsync(_offset: number | undefined = undefined) {
    if (p_roomId === null)
        return;

    const data = await p_storageApi.getDataAsync(p_roomId, undefined, _offset);
    if (data === null || !data.Success)
        return;

    p_lastOffset = data.LastUpdateUnixMs;

    const usersMap = groupBy(data.Entries, _ => _.Username);
    const users = Object.keys(usersMap);

    // notify about new users
    if (p_firstDataReceived)
        for (let user of users)
            if (p_paths.get(user) === undefined)
                sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_NEW_TRACK, data: user });

    // init users controls
    for (let user of users)
        initControlsForUser(user);

    // update users controls
    for (let user of users) {
        const userData = usersMap[user];
        updateControlsForUser(user, userData, _offset === undefined);
    }

    const userAgent = navigator.userAgent;
    if (!p_firstDataReceived) {
        p_firstDataReceived = true;
        if (!userAgent.includes("RoadnikApp")) {
            console.log("Not roadnik app, setting default view...");
            setViewToAllTracks();
        }
        else {
            p_map.attributionControl.remove();
            sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_INITIAL_DATA_RECEIVED, data: {} });
        }
    }

    document.title = `Roadnik: ${p_roomId} (${p_paths.size})`;
}

function initControlsForUser(_user: string): void {
    const color = Maps.Colors[p_userColorIndex % Maps.Colors.length];
    const colorFile = `img/map_icon_${p_userColorIndex}.png`;

    if (p_lastAlts.get(_user) === undefined)
        p_lastAlts.set(_user, 0);
    if (p_markers.get(_user) === undefined) {
        const icon = L.icon({
            iconUrl: colorFile,
            iconSize: [40, 40],
            iconAnchor: [20, 40],
            popupAnchor: [0, -40]
        });
        const marker = L.marker([51.4768, 0.0006], { title: _user, icon: icon })
            .addTo(p_map)
            .bindPopup("<b>Unknown track!</b>")
            .addEventListener('popupopen', () => sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_POPUP_OPENED, data: _user }))
            .addEventListener('popupclose', () => sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_POPUP_CLOSED, data: _user }));

        p_markers.set(_user, marker);
    }
    if (p_circles.get(_user) === undefined)
        p_circles.set(_user, L.circle([51.4768, 0.0006], 100, { color: color, fillColor: '*', fillOpacity: 0.3 })
            .addTo(p_map));
    if (p_paths.get(_user) === undefined)
        p_paths.set(_user, L.polyline([], { color: color, smoothFactor: 1, weight: 5 })
            .addTo(p_map));

    p_userColorIndex++;
}

function updateControlsForUser(
    _user: string,
    _entries: Api.TimedStorageEntry[],
    _firstUpdate: boolean): void {
    const lastEntry = _entries[_entries.length - 1];
    if (lastEntry === undefined)
        return;

    const lastLocation = new L.LatLng(lastEntry.Latitude, lastEntry.Longitude, lastEntry.Altitude);

    const circle = p_circles.get(_user);
    if (circle !== undefined) {
        circle.setLatLng(lastLocation);
        circle.setRadius(lastEntry.Accuracy ?? 100);
        circle.bringToFront();
    }

    const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - lastEntry.UnixTimeMs);
    let elapsedString = "now";
    if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
        elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

    const kmh = (lastEntry.Speed ?? 0) * 3.6;

    let altChangeMark = "\u2192";
    const lastAlt = p_lastAlts.get(_user);
    const newAlt = Math.ceil(lastEntry.Altitude);
    if (lastAlt !== undefined) {
        if (newAlt > lastAlt)
            altChangeMark = "\u2191";
        else if (newAlt < lastAlt)
            altChangeMark = "\u2193";
    }
    p_lastAlts.set(_user, newAlt);

    const popUpText =
        `<b>${_user}</b>: ${lastEntry.Message ?? "Hi!"}
        </br>
        <p>
        Speed: ${kmh.toFixed(2)} km/h
        </br>
        Altitude ( ${altChangeMark} ): ${newAlt} m
        </br>
        Heading: ${Math.round(lastEntry.Bearing ?? -1)}°
        </p>
        <p>
        Battery: ${lastEntry.Battery}%
        </br>
        GSM power: ${lastEntry.GsmSignal}%
        <br/>
        Updated ${elapsedString}
        </p>`;

    const marker = p_markers.get(_user);
    if (marker !== undefined) {
        marker.setLatLng(lastLocation);
        marker.setPopupContent(popUpText);

        if (marker.isPopupOpen())
            p_map.flyTo(lastLocation);
    }

    const path = p_paths.get(_user);
    if (path !== undefined) {
        const points = _entries.map(_x => new L.LatLng(_x.Latitude, _x.Longitude, _x.Altitude));
        if (_firstUpdate === true)
            path.setLatLngs(points);
        else
            for (let point of points)
                path.addLatLng(point);
    }
}

if (p_roomId !== null) {
    const ws = p_storageApi.setupWs(p_roomId, (_ws, _data) => {
        if (_data.Type === Api.WS_MSG_TYPE_HELLO || _data.Type === Api.WS_MSG_TYPE_DATA_UPDATED)
            updateViewAsync(p_lastOffset);
    });
}

// C# interaction
function sendDataToHost(_msg: JsToCSharpMsg): void {
    try {
        (window as any).jsBridge.invokeAction(JSON.stringify(_msg));
    }
    catch { }
}

// exports for C#
function setLocation(_x: number, _y: number, _zoom?: number | undefined): boolean {
    p_map.flyTo([_x, _y], _zoom, { duration: 0.5 });
    return true;
}
(window as any).setLocation = setLocation;

function setViewToAllTracks(): boolean {
    if (p_paths.size > 0) {
        let bounds: L.LatLngBoundsExpression | undefined = undefined;
        for (let path of p_paths.values())
            if (bounds === undefined)
                bounds = path.getBounds();
            else
                bounds = bounds.extend(path.getBounds());

        if (bounds !== undefined)
            p_map.fitBounds(bounds);
    }

    return true;
}
(window as any).setViewToAllTracks = setViewToAllTracks;

function setMapLayer(_mapLayer?: string | undefined | null): boolean {
    var layer = p_mapsData.array.find((_v, _i, _o) => _v.name === _mapLayer);
    if (layer !== undefined)
        layer.tileLayer.addTo(p_map);

    return true;
}
(window as any).setMapLayer = setMapLayer;

function getMapViewState(): MapViewState {
    return {
        location: p_map.getCenter(),
        zoom: p_map.getZoom(),
    };
}
(window as any).getState = getMapViewState;

function setViewToTrack(_pathName: string, _zoom: number): boolean {
    if (p_markers.size > 0) {
        const marker = p_markers.get(_pathName);
        if (marker === undefined)
            return false;

        p_map.flyTo(marker.getLatLng(), _zoom, { duration: 0.5 });
        marker.openPopup();
    }

    return true;
}
(window as any).setViewToTrack = setViewToTrack;

sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_APP_LOADED, data: {} });