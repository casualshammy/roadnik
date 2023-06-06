import * as L from "leaflet"
import * as Api from "./modules/api";
import { TimeSpan } from "./modules/timespan";
import { HOST_MSG_NEW_POINT, JsToCSharpMsg, MapViewState, TimedStorageEntry, WsMsgPathWiped } from "./modules/api";
import { NumberDictionary, Pool, StringDictionary, groupBy } from "./modules/toolkit";
import { LeafletMouseEvent } from "leaflet";
import Cookies from "js-cookie";
import { COOKIE_MAP_LAYER, COOKIE_SELECTED_USER } from "./modules/consts";
import { DEFAULT_MAP_LAYER, GetMapLayers, GetMapOverlayLayers, PathColors } from "./modules/maps";

const p_storageApi = new Api.StorageApi();

const queryString = window.location.search;
const urlParams = new URLSearchParams(queryString);
const p_roomId = urlParams.get('id');

const p_isRoadnikApp = navigator.userAgent.includes("RoadnikApp");

const p_markers = new Map<string, L.Marker>();
const p_circles = new Map<string, L.Circle>();
const p_paths = new Map<string, L.Polyline>();
const p_geoEntries: StringDictionary<TimedStorageEntry[]> = {};
const p_pointMarkers: NumberDictionary<L.Marker> = [];
const p_pointMarkersPool = new Pool<L.Marker>(() => L.marker([0, 0]));

let p_firstDataReceived = false;
let p_lastOffset = 0;
let p_currentLayer: string | undefined = undefined;
let p_userColorIndex = 0;
let p_pointsDataReceived: boolean = false;

const p_mapsData = GetMapLayers();
const p_overlays = GetMapOverlayLayers();

const p_map = new L.Map('map', {
    center: new L.LatLng(51.4768, 0.0006),
    zoom: 14,
    layers: [p_mapsData[DEFAULT_MAP_LAYER]]
});

p_map.attributionControl.setPrefix(false);
if (p_isRoadnikApp)
    p_map.attributionControl.remove();

p_map.on('baselayerchange', function (_e) {
    p_currentLayer = _e.name;
    if (!p_isRoadnikApp)
        Cookies.set(COOKIE_MAP_LAYER, _e.name);

    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED, data: p_currentLayer });
});
p_map.on('zoomend', function (_e) {
    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED, data: getMapViewState() });
});
p_map.on('moveend', function (_e) {
    sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED, data: getMapViewState() });
});
p_map.on("contextmenu", function (_e) {
    if (p_roomId === null)
        return;

    console.log(`Initializing waypoint in ${_e.latlng}...`);
    if (p_isRoadnikApp) {
        sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED, data: _e.latlng });
    }
    else {
        const msg = prompt("Please enter a description for point:");
        if (msg !== null)
            p_storageApi.createRoomPointAsync(p_roomId, "", _e.latlng, msg);
    }
});

const cookieLayout = Cookies.get(COOKIE_MAP_LAYER);
if (p_isRoadnikApp || cookieLayout === undefined || !setMapLayer(cookieLayout))
    p_currentLayer = DEFAULT_MAP_LAYER;

const p_layersControl = new L.Control.Layers(p_mapsData, p_overlays);
p_map.addControl(p_layersControl);

async function updateViewAsync(_offset: number | undefined = undefined) {
    if (p_roomId === null)
        return;

    const data = await p_storageApi.getDataAsync(p_roomId, _offset);
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

    if (!p_firstDataReceived) {
        p_firstDataReceived = true;
        if (!p_isRoadnikApp) {
            const cookieSelectedUser = Cookies.get(COOKIE_SELECTED_USER);
            if (cookieSelectedUser === undefined || !setViewToTrack(cookieSelectedUser, p_map.getZoom())) {
                console.log("Not roadnik app, setting default view...");
                setViewToAllTracks();
            }
        }
        else {
            sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_INITIAL_DATA_RECEIVED, data: {} });
        }
    }

    document.title = `Roadnik: ${p_roomId} (${p_paths.size})`;
}

function initControlsForUser(_user: string): void {
    const color = PathColors[p_userColorIndex % PathColors.length];
    const colorFile = `img/map_icon_${p_userColorIndex}.png`;

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
            .addEventListener('popupopen', () => {
                if (!p_isRoadnikApp)
                    Cookies.set(COOKIE_SELECTED_USER, _user);

                sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_POPUP_OPENED, data: _user });
            })
            .addEventListener('popupclose', () => {
                if (!p_isRoadnikApp)
                    Cookies.remove(COOKIE_SELECTED_USER);

                sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_POPUP_CLOSED, data: _user });
            });

        p_markers.set(_user, marker);
    }
    if (p_circles.get(_user) === undefined)
        p_circles.set(_user, L.circle([51.4768, 0.0006], 100, { color: color, fillColor: '*', fillOpacity: 0.3 })
            .addTo(p_map));
    if (p_paths.get(_user) === undefined) {
        const path = L.polyline([], { color: color, smoothFactor: 1, weight: 6 })
            .addTo(p_map)
            .bindPopup("")
            .addEventListener("click", (_ev: LeafletMouseEvent) => {
                const entries = p_geoEntries[_user];
                if (entries === undefined)
                    return;

                let nearestLatLng: L.LatLng | undefined = undefined;
                let nearestEntry: Api.TimedStorageEntry | undefined = undefined;
                for (let entry of entries) {
                    const latLng = new L.LatLng(entry.Latitude, entry.Longitude, entry.Altitude);
                    if (nearestLatLng === undefined || _ev.latlng.distanceTo(latLng) < _ev.latlng.distanceTo(nearestLatLng)) {
                        nearestLatLng = latLng;
                        nearestEntry = entry;
                    }
                }

                if (nearestLatLng !== undefined && nearestEntry !== undefined) {
                    const popupText = buildPathPointPopup(_user, nearestEntry);
                    path.setPopupContent(popupText);
                    path.openPopup(nearestLatLng);
                }
            });

        p_paths.set(_user, path);
    }

    if (p_geoEntries[_user] === undefined)
        p_geoEntries[_user] = [];

    p_userColorIndex++;
}

function updateControlsForUser(
    _user: string,
    _entries: Api.TimedStorageEntry[],
    _firstUpdate: boolean): void {
    const lastEntry = _entries[_entries.length - 1];
    if (lastEntry === undefined)
        return;

    var geoEntries = p_geoEntries[_user];
    if (geoEntries !== undefined) {
        geoEntries.push(..._entries);
        const geoEntriesExcessiveCount = geoEntries.length - 1000;
        if (geoEntriesExcessiveCount > 0) {
            const removedEntries = geoEntries.splice(0, geoEntriesExcessiveCount);
            console.log(`${removedEntries.length} geo entries were removed for user ${_user}`);
        }
    }

    const lastLocation = new L.LatLng(lastEntry.Latitude, lastEntry.Longitude, lastEntry.Altitude);

    const circle = p_circles.get(_user);
    if (circle !== undefined) {
        circle.setLatLng(lastLocation);
        circle.setRadius(lastEntry.Accuracy ?? 100);
        circle.bringToFront();
    }

    const popUpText = buildPathPointPopup(_user, lastEntry);

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

function buildPathPointPopup(_user: string, _entry: Api.TimedStorageEntry): string {
    const kmh = (_entry.Speed ?? 0) * 3.6;

    const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - _entry.UnixTimeMs);
    let elapsedString = "now";
    if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
        elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

    const popUpText =
        `<b>${_user}</b>: ${_entry.Message ?? "Hi!"}
        </br>
        <p>
        ${kmh.toFixed(2)} km/h @ ${Math.ceil(_entry.Altitude)} m @ ${Math.round(_entry.Bearing ?? -1)}°
        </br>
        Accuracy: ${Math.ceil(_entry.Accuracy ?? 100)} m
        </p>
        <p>
        Battery: ${_entry.Battery}%
        </br>
        GSM power: ${_entry.GsmSignal}%
        <br/>
        Updated ${elapsedString}
        </p>`;

    return popUpText;
}

async function updatePointsAsync() {
    if (p_roomId === null)
        return;

    const data = await p_storageApi.listRoomPointsAsync(p_roomId);
    if (data === null)
        return;

    const allPointIds = Object.keys(p_pointMarkers).map(_ => +_);
    const validPointIds: number[] = [];

    for (let entry of data) {
        validPointIds.push(entry.PointId);

        let text: string;
        if (entry.Username.length > 0)
            text = `<strong>${entry.Username}:</strong><br/>${entry.Description}`;
        else
            text = entry.Description;

        let marker = p_pointMarkers[entry.PointId];
        if (marker === undefined) {
            marker = p_pointMarkersPool.resolve();
            p_pointMarkers[entry.PointId] = marker;

            marker.setLatLng([entry.Lat, entry.Lng])
            marker.addTo(p_map);
            marker.bindPopup(text);
            marker.on("contextmenu", function (_e) {
                p_storageApi.deleteRoomPointAsync(p_roomId, entry.PointId);
            });

            if (p_pointsDataReceived)
                sendDataToHost({ msgType: HOST_MSG_NEW_POINT, data: entry });
        }
    }

    for (let pointId of allPointIds) {
        if (validPointIds.includes(pointId))
            continue;

        let marker = p_pointMarkers[pointId];
        marker.remove();
        marker.unbindPopup();
        marker.off("contextmenu");

        delete p_pointMarkers[pointId];
        p_pointMarkersPool.free(marker);
    }

    p_pointsDataReceived = true;
    console.log(`Points visible: ${validPointIds.length}; points in pool: ${p_pointMarkersPool.getAvailableCount()}`);
}

if (p_roomId !== null) {
    const ws = p_storageApi.setupWs(p_roomId, (_ws, _data) => {
        if (_data.Type === Api.WS_MSG_TYPE_HELLO) {
            updateViewAsync(p_lastOffset);
            updatePointsAsync();
        }
        else if (_data.Type === Api.WS_MSG_TYPE_DATA_UPDATED) {
            updateViewAsync(p_lastOffset);
        }
        else if (_data.Type == Api.WS_MSG_PATH_WIPED) {
            const msgData: WsMsgPathWiped = _data.Payload;
            const user = msgData.Username;

            const path = p_paths.get(user);
            if (path !== undefined)
                path.setLatLngs([]);

            const geoEntries = p_geoEntries[user];
            if (geoEntries !== undefined)
                geoEntries.length = 0;
        }
        else if (_data.Type == Api.WS_MSG_ROOM_POINTS_UPDATED) {
            console.log("Points were changed, updating markers...");
            updatePointsAsync();
        }
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
    if (_mapLayer === undefined || _mapLayer === null)
        return false;

    var layer = p_mapsData[_mapLayer];
    if (layer !== undefined) {
        layer.addTo(p_map);
        return true;
    }
    return false;
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
    const marker = p_markers.get(_pathName);
    if (marker === undefined)
        return false;

    p_map.flyTo(marker.getLatLng(), _zoom, { duration: 0.5 });
    marker.openPopup();
    return true;
}
(window as any).setViewToTrack = setViewToTrack;

sendDataToHost({ msgType: Api.JS_TO_CSHARP_MSG_TYPE_APP_LOADED, data: {} });
