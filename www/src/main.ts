import * as L from "leaflet"
import * as Api from "./modules/api";
import * as Maps from "./modules/maps"
import { TimeSpan } from "./modules/timespan";
import { WebAppState } from "./modules/api";
import { groupBy } from "./modules/toolkit";

const p_storageApi = new Api.StorageApi();

async function refreshPositionFullAsync(_key: string, _offset: number | undefined = undefined) {
    const data = await p_storageApi.getDataAsync(_key, undefined, _offset);
    if (data === null || !data.Success)
        return;

    p_lastOffset = data.LastUpdateUnixMs;

    const usersMap = groupBy(data.Entries, _ => _.Nickname);
    const users = Object.keys(usersMap);

    // init users controls
    let colorIndex = 0;
    for (let user of users) {
        const color = Maps.Colors[colorIndex % Maps.Colors.length];
        const colorFile = `img/map_icon_${colorIndex}.png`;

        if (p_lastAlts.get(user) === undefined)
            p_lastAlts.set(user, 0);
        if (p_markers.get(user) === undefined) {
            var icon = L.icon({
                iconUrl: colorFile,
                iconSize: [40, 40],
                iconAnchor: [20, 40],
                popupAnchor: [0, -40]
            });
            p_markers.set(user, L.marker([51.4768, 0.0006], { title: user, icon: icon})
                .addTo(map)
                .bindPopup("<b>Unknown track!</b>")
                .openPopup());
        }
        if (p_circles.get(user) === undefined)
            p_circles.set(user, L.circle([51.4768, 0.0006], 100, { color: color, fillColor: '*', fillOpacity: 0.3 })
                .addTo(map));
        if (p_paths.get(user) === undefined)
            p_paths.set(user, L.polyline([], { color: color, smoothFactor: 1, weight: 5 })
                .addTo(map));

        colorIndex++;
    }

    if (p_paths.size > 1) {
        p_autoPan = false;
        autoPanCheckbox.remove();
    }

    // update users controls
    for (let user of users) {
        const userData = usersMap[user];
        const lastEntry = userData[userData.length - 1];
        if (lastEntry === undefined)
            continue;

        const lastLocation = new L.LatLng(lastEntry.Latitude, lastEntry.Longitude, lastEntry.Altitude);

        const circle = p_circles.get(user);
        if (circle !== undefined) {
            circle.setLatLng(lastLocation);
            circle.setRadius(lastEntry.Accuracy ?? 100);
            circle.bringToFront();
        }

        if (!p_firstCentered) {
            map.setView(lastLocation, 15);
            p_firstCentered = true;
        }

        const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - data.LastUpdateUnixMs);
        let elapsedString = "now";
        if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
            elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

        const kmh = (lastEntry.Speed ?? 0) * 3.6;

        let altChangeMark = "\u2192";
        const lastAlt = p_lastAlts.get(user);
        if (lastAlt !== undefined) {
            if (lastEntry.Altitude > lastAlt)
                altChangeMark = "\u2191";
            else if (lastEntry.Altitude < lastAlt)
                altChangeMark = "\u2193";
        }
        p_lastAlts.set(user, lastEntry.Altitude);

        const popUpText =
            `<b>${user}</b>: ${lastEntry.Message ?? "Hi!"}
            </br>
            <p>
            Speed: ${kmh.toFixed(2)} km/h
            </br>
            Altitude ( ${altChangeMark} ): ${lastEntry.Altitude} m
            </br>
            Heading: ${lastEntry.Bearing} degrees
            </p>
            <p>
            Battery: ${lastEntry.Battery}%
            </br>
            GSM power: ${lastEntry.GsmSignal}%
            <br/>
            Updated ${elapsedString}
            </p>`;

        const marker = p_markers.get(user);
        if (marker !== undefined) {
            marker.setLatLng(lastLocation);
            marker.setPopupContent(popUpText);
        }

        if (p_autoPan === true)
            map.flyTo(lastLocation);

        const path = p_paths.get(user);
        if (path !== undefined) {
            const points = userData.map(_x => new L.LatLng(_x.Latitude, _x.Longitude, _x.Altitude));
            if (_offset === undefined)
                path.setLatLngs(points);
            else
                for (let point of points)
                    path.addLatLng(point);
        }
    }
}

const queryString = window.location.search;
const urlParams = new URLSearchParams(queryString);
const key = urlParams.get('key');

let p_lastAlts = new Map<string, number>();
let p_markers = new Map<string, L.Marker>();
let p_circles = new Map<string, L.Circle>();
let p_paths = new Map<string, L.Polyline>();

let p_firstCentered = false;
let p_autoPan = false;
let p_lastOffset = 0;
let p_currentLayer: string | undefined = undefined;

const mapsData = Maps.GetMapLayers();
const overlays = Maps.GetMapOverlayLayers();

const map = new L.Map('map', {
    center: new L.LatLng(51.4768, 0.0006),
    zoom: 14,
    layers: [mapsData.array[0].tileLayer]
});
map.on('baselayerchange', function (_e) {
    p_currentLayer = _e.name;
    sendDataToHost(JSON.stringify(getState()));
});
p_currentLayer = mapsData.array[0].name;

const layersControl = new L.Control.Layers(mapsData.obj, overlays);
map.addControl(layersControl);

const autoPanCheckbox = Maps.GetCheckBox("Auto track", 'topleft', _checked => {
    p_autoPan = _checked;
    sendDataToHost(JSON.stringify(getState()));
});
autoPanCheckbox.addTo(map);

if (key !== null) {
    const ws = p_storageApi.setupWs(key, (_ws, _data) => {
        if (_data.Type === Api.WS_MSG_TYPE_HELLO)
            refreshPositionFullAsync(key);

        if (_data.Type === Api.WS_MSG_TYPE_DATA_UPDATED)
            refreshPositionFullAsync(key, p_lastOffset);
    });
}

// C# interaction
function sendDataToHost(_data: string): void {
    try {
        (window as any).jsBridge.invokeAction(_data);
    }
    catch { }
}

// exports for C#
function setLocation(_x: number, _y: number): void {
    map.flyTo([_x, _y]);
}
(window as any).setLocation = setLocation;

function getState(): WebAppState {
    return {
        location: map.getCenter(),
        zoom: map.getZoom(),
        mapLayer: p_currentLayer,
        autoPan: p_autoPan
    };
}
(window as any).getState = getState;

function setState(_state: WebAppState): boolean {
    p_autoPan = _state.autoPan;
    autoPanCheckbox.setChecked(p_autoPan);

    map.flyTo(_state.location, _state.zoom);

    if (_state.mapLayer !== undefined) {
        var layer = mapsData.array.find((_v, _i, _o) => _v.name == _state.mapLayer);
        if (layer !== undefined)
            layer.tileLayer.addTo(map);
    }

    return true;
}
(window as any).setState = setState;
