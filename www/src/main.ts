import * as L from "leaflet"
import * as Api from "./modules/api";
import { TimeSpan } from "./modules/timespan";
import { HostMsgTracksSynchronizedData, JsToCSharpMsg, TimedStorageEntry, WsMsgPathWiped } from "./modules/api";
import { Pool, base64ToUtf8Text, byteArrayToHexString, colorNameToRgba, groupBy, makeDraggableBottomLeft, sleepAsync } from "./modules/toolkit";
import { LeafletMouseEvent } from "leaflet";
import Cookies from "js-cookie";
import { CLASS_IS_DRAGGING, COOKIE_MAP_LAYER, COOKIE_MAP_STATE, COOKIE_SELECTED_PATH_BOTTOM, COOKIE_SELECTED_PATH_LEFT, COOKIE_SELECTED_PATH, HOST_MSG_TRACKS_SYNCHRONIZED, JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED, TRACK_COLORS, WS_MSG_PATH_WIPED, WS_MSG_ROOM_POINTS_UPDATED, WS_MSG_TYPE_DATA_UPDATED, WS_MSG_TYPE_HELLO, COOKIE_MAP_OVERLAY, HOST_MSG_MAP_STATE } from "./modules/consts";
import { DEFAULT_MAP_LAYER, GenerateCircleIcon, GeneratePulsatingCircleIcon, GetMapLayers, GetMapOverlayLayers, GetMapStateFromCookie } from "./modules/maps";
import { Subject, concatMap, scan, switchMap, asyncScheduler, observeOn } from "rxjs";
import { CreateAppCtx } from "./modules/parts/AppCtx";
import Swal from "sweetalert2";

const mapsData = GetMapLayers();
const mapOverlays = GetMapOverlayLayers();

const p_storageApi = new Api.StorageApi();
const p_appCtx = CreateAppCtx(mapsData, mapOverlays);

const p_markers = new Map<string, L.Marker>();
const p_circles = new Map<string, L.Circle>();
const p_paths = new Map<string, L.Polyline>();
const p_geoEntries: { [userName: string]: TimedStorageEntry[] } = {};
const p_pointMarkers: { [key: number]: L.Marker } = {};
const p_pointMarkersPool = new Pool<L.Marker>(() => L.marker([0, 0]));
const p_tracksUpdateRequired$ = new Subject<void>();

const p_map = initMap();

function initMap(): L.Map {
  const map = new L.Map('map', {
    center: new L.LatLng(
      p_appCtx.mapState.lat,
      p_appCtx.mapState.lng),
    zoom: p_appCtx.mapState.zoom,
    layers: [mapsData[p_appCtx.mapState.layer]],
    zoomControl: false
  });

  for (const overlay of p_appCtx.mapState.overlays)
    map.addLayer(mapOverlays[overlay]);

  map.attributionControl.setPrefix(false);
  if (p_appCtx.isRoadnikApp)
    map.attributionControl.remove();

  map.on('baselayerchange', function (_e) {
    p_appCtx.mapState.layer = _e.name;

    console.log(`Layer changed to ${p_appCtx.mapState.layer}`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(COOKIE_MAP_LAYER, p_appCtx.mapState.layer);
    else
      sendMapStateToRoadnikApp();
  });
  map.on('overlayadd', function (_e) {
    const overlay = _e.name;
    if (!p_appCtx.mapState.overlays.includes(overlay))
      p_appCtx.mapState.overlays.push(overlay);

    console.log(`Added overlay '${overlay}'`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(COOKIE_MAP_OVERLAY, JSON.stringify(p_appCtx.mapState.overlays));
    else
      sendMapStateToRoadnikApp();
  });
  map.on('overlayremove', function (_e) {
    const overlay = _e.name;
    p_appCtx.mapState.overlays = p_appCtx.mapState.overlays.filter((_v, _i, _) => _v !== overlay);

    console.log(`Removed overlay '${overlay}'`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(COOKIE_MAP_OVERLAY, JSON.stringify(p_appCtx.mapState.overlays));
    else
      sendMapStateToRoadnikApp();
  });
  map.on('zoomend', function (_e) {
    const location = map.getCenter();

    p_appCtx.mapState.lat = location.lat;
    p_appCtx.mapState.lng = location.lng;
    p_appCtx.mapState.zoom = map.getZoom();

    if (!p_appCtx.isRoadnikApp) {
      const stateString = `${p_appCtx.mapState.lat}:${p_appCtx.mapState.lng}:${p_appCtx.mapState.zoom}`;
      Cookies.set(COOKIE_MAP_STATE, stateString);
    }
    else {
      sendMapStateToRoadnikApp();
    }
  });
  map.on('moveend', function (_e) {
    const location = map.getCenter();

    p_appCtx.mapState.lat = location.lat;
    p_appCtx.mapState.lng = location.lng;
    p_appCtx.mapState.zoom = map.getZoom();

    if (!p_appCtx.isRoadnikApp) {
      const stateString = `${p_appCtx.mapState.lat}:${p_appCtx.mapState.lng}:${p_appCtx.mapState.zoom}`;
      Cookies.set(COOKIE_MAP_STATE, stateString);
    }
    else {
      sendMapStateToRoadnikApp();
    }
  });
  map.on("contextmenu", function (_e) {
    if (p_appCtx.roomId === null)
      return;

    console.log(`Initializing waypoint in ${_e.latlng}...`);
    if (p_appCtx.isRoadnikApp) {
      sendDataToHost({ msgType: JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED, data: _e.latlng });
    }
    else {
      const msg = prompt("Please enter a description for point:");
      if (msg !== null)
        p_storageApi.createRoomPointAsync(p_appCtx.roomId, "", _e.latlng, msg);
    }
  });

  L.control.scale({
    position: 'bottomright',
    maxWidth: 200,
    metric: true,
    imperial: false,
    updateWhenIdle: true
  }).addTo(map);

  L.control.layers(
    mapsData, mapOverlays
  ).addTo(map);

  L.control.zoom({
    position: 'topright'
  }).addTo(map);

  return map;
}

async function updatePathsAsync() {
  if (p_appCtx.roomId === null)
    return;

  let data: Api.GetPathResData;
  try {
    data = await p_storageApi.getDataAsync(p_appCtx.roomId, p_appCtx.lastTracksOffset);
  } catch (error) {
    console.warn(`Got error trying to fetch paths data with offset ${p_appCtx.lastTracksOffset}, retrying...\n${error}`);
    await sleepAsync(1000);
    p_tracksUpdateRequired$.next();
    return;
  }

  if (data === null)
    return;

  const prevOffset = p_appCtx.lastTracksOffset;
  p_appCtx.lastTracksOffset = data.LastUpdateUnixMs;
  console.log(`New last offset: ${p_appCtx.lastTracksOffset}; points to process: ${data.Entries.length}`);

  const usersMap = groupBy(data.Entries, _ => _.Username);
  const users = Object.keys(usersMap);

  // init users controls
  for (let user of users)
    initControlsForUser(user);

  // update users controls
  for (let user of users) {
    const userData = usersMap[user];
    updateControlsForUser(user, userData, prevOffset === 0);
  }

  document.title = `Roadnik: ${p_appCtx.roomId} (${p_paths.size})`;
  if (data.MoreEntriesAvailable) {
    p_tracksUpdateRequired$.next();
    return;
  }

  if (!p_appCtx.firstTracksSyncCompleted) {
    const selectedPath = p_appCtx.mapState.selectedPath;
    if (selectedPath === null) {
      console.log("Initial selected path is not set, setting default view...");
      setViewToAllTracks();
    }
    else if (!setViewToTrack(selectedPath, p_map.getZoom())) {
      console.log("Initial selected path is set but not found, setting default view...");
      setViewToAllTracks();
    }
    else {
      console.log(`Initial selected path is ${selectedPath}`);
      updateSelectedPath(selectedPath);
    }

    if (p_appCtx.isRoadnikApp) {
      const msgData: HostMsgTracksSynchronizedData = {
        isFirstSync: true
      };
      sendDataToHost({ msgType: HOST_MSG_TRACKS_SYNCHRONIZED, data: msgData });
    }

    p_appCtx.firstTracksSyncCompleted = true;
  }
  else {
    const msgData: HostMsgTracksSynchronizedData = {
      isFirstSync: false
    };
    sendDataToHost({ msgType: HOST_MSG_TRACKS_SYNCHRONIZED, data: msgData });
  }
}

function initControlsForUser(_user: string): void {
  let color = p_appCtx.userColors.get(_user);
  if (color === undefined) {
    color = TRACK_COLORS[p_appCtx.userColorIndex++ % TRACK_COLORS.length];
    p_appCtx.userColors.set(_user, color);
  }

  if (p_markers.get(_user) === undefined) {
    const icon = GeneratePulsatingCircleIcon(15, color);
    const marker = L.marker([51.4768, 0.0006], { title: _user, icon: icon })
      .addTo(p_map)
      .addEventListener('click', () => {
        updateSelectedPath(_user);
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
          const popupText = buildPathPointPopup(_user, nearestEntry, false);
          path.setPopupContent(popupText);
          path.openPopup(nearestLatLng);
        }
      });

    p_paths.set(_user, path);
  }

  if (p_geoEntries[_user] === undefined)
    p_geoEntries[_user] = [];
}

function updateControlsForUser(
  _user: string,
  _entries: Api.TimedStorageEntry[],
  _isFirstDataChunk: boolean): void {
  const lastEntry = _entries[_entries.length - 1];
  if (lastEntry === undefined)
    return;

  const geoEntries = p_geoEntries[_user];
  if (geoEntries !== undefined) {
    geoEntries.push(..._entries);
    const geoEntriesExcessiveCount = geoEntries.length - p_appCtx.maxTrackPoints;
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

  const marker = p_markers.get(_user);
  if (marker !== undefined)
    marker.setLatLng(lastLocation);

  if (p_appCtx.mapState.selectedPath === _user) {
    updateSelectedPath(_user);
    p_map.flyTo(lastLocation);
  }

  const path = p_paths.get(_user);
  if (path !== undefined) {
    const points = _entries.map(_x => new L.LatLng(_x.Latitude, _x.Longitude, _x.Altitude));
    if (_isFirstDataChunk) {
      path.setLatLngs(points);
      console.log(`Set ${points.length} points to path ${_user}`);
    }
    else {
      for (const point of points)
        path.addLatLng(point);

      console.log(`Added ${points.length} points to path ${_user}`);
    }
  }
}

function buildPathPointPopup(_user: string, _entry: Api.TimedStorageEntry, _addCloseBtn: boolean): string {
  const kmh = (_entry.Speed ?? 0) * 3.6;

  const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - _entry.UnixTimeMs);
  let elapsedString = "now";
  if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
    elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

  const popUpText =
    `${_addCloseBtn ? '<a href="#" onclick="updateSelectedPath(null)" title="Close" id="selected-path-close">✘</a>' : ''}
        <center>
            <b>${_user}</b> (${elapsedString})
            </br>
            🔋${((_entry.Battery ?? 0) * 100).toFixed(0)}% 📶${((_entry.GsmSignal ?? 0) * 100).toFixed(0)}%
        </center>
        <p style="margin-bottom: 0px">
        🚀${kmh.toFixed(1)} km/h ⛰${Math.ceil(_entry.Altitude)} m 📡${Math.ceil(_entry.Accuracy ?? 100)} m
        </p>`;

  return popUpText;
}

function updateSelectedPath(_user: string | null, _log: boolean = true) {
  const div = document.getElementById("selected-path-container") as HTMLDivElement;

  p_appCtx.mapState.selectedPath = _user;

  if (_user === null) {
    div.hidden = true;

    if (!p_appCtx.isRoadnikApp)
      Cookies.remove(COOKIE_SELECTED_PATH);
    else
      sendMapStateToRoadnikApp();

    if (_log)
      console.log(`Selected path is cleared`);

    return;
  }

  if (!p_appCtx.isRoadnikApp)
    Cookies.set(COOKIE_SELECTED_PATH, _user);
  else
    sendMapStateToRoadnikApp();

  if (_log)
    console.log(`Selected path is set to ${_user}`);

  if (div.classList.contains(CLASS_IS_DRAGGING))
    return;

  const geoEntries = p_geoEntries[_user];
  if (geoEntries !== undefined) {
    const lastEntry = geoEntries[geoEntries.length - 1];
    if (lastEntry !== undefined) {
      const text = buildPathPointPopup(_user, lastEntry, true);
      div.innerHTML = text;

      const color = p_appCtx.userColors.get(_user);
      if (color !== undefined) {
        div.style.borderColor = color;

        const colorBytes = colorNameToRgba(color);
        if (colorBytes !== null) {
          const bgR = Math.min(128 + colorBytes[0] / 4, 255);
          const bgG = Math.min(128 + colorBytes[1] / 4, 255);
          const bgB = Math.min(128 + colorBytes[2] / 4, 255);
          div.style.backgroundColor = `#${byteArrayToHexString([bgR, bgG, bgB])}`;
        }

        const closeBtn = document.getElementById("selected-path-close");
        if (closeBtn !== null)
          closeBtn.style.background = color;
      }

      div.hidden = false;
    }
  }
}
(window as any).updateSelectedPath = updateSelectedPath;

async function updatePointsAsync() {
  if (p_appCtx.roomId === null)
    return;

  const data = await p_storageApi.listRoomPointsAsync(p_appCtx.roomId);
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
        p_storageApi.deleteRoomPointAsync(p_appCtx.roomId!, entry.PointId);
      });
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

  console.log(`Points visible: ${validPointIds.length}; points in pool: ${p_pointMarkersPool.getAvailableCount()}`);
}

function onStart() {
  const selectedPathContainer = document.getElementById("selected-path-container");
  if (selectedPathContainer !== null) {
    makeDraggableBottomLeft(selectedPathContainer, (_left, _bottom) => {
      p_appCtx.mapState.selectedPathWindowLeft = _left;
      p_appCtx.mapState.selectedPathWindowBottom = _bottom;

      if (!p_appCtx.isRoadnikApp) {
        Cookies.set(COOKIE_SELECTED_PATH_LEFT, _left.toString());
        Cookies.set(COOKIE_SELECTED_PATH_BOTTOM, _bottom.toString());
      }
      else {
        sendMapStateToRoadnikApp();
      }
    });

    const left = p_appCtx.mapState.selectedPathWindowLeft;
    if (left !== null)
      selectedPathContainer.style.left = left + "px";

    const bottom = p_appCtx.mapState.selectedPathWindowBottom;
    if (bottom !== null)
      selectedPathContainer.style.bottom = bottom + "px";

    selectedPathContainer.addEventListener('dblclick', (_ev: MouseEvent) => {
      const path = p_appCtx.mapState.selectedPath;
      if (path !== null)
        setViewToTrack(path, p_map.getZoom());
    });
  }

  p_tracksUpdateRequired$
    .pipe(
      observeOn(asyncScheduler),
      switchMap(async _ => await updatePathsAsync()))
    .subscribe();

  if (p_appCtx.roomId !== null) {
    const ws = p_storageApi.setupWs(p_appCtx.roomId, async (_ws, _data) => {
      console.log(`WS MSG: ${_data.Type}`);
      if (_data.Type === WS_MSG_TYPE_HELLO) {
        const msgData: Api.WsMsgHello = _data.Payload;
        p_appCtx.maxTrackPoints = msgData.MaxPathPointsPerRoom;
        console.log(`Max saved points: ${p_appCtx.maxTrackPoints}`);
        console.log(`Server time: ${new Date(msgData.UnixTimeMs).toISOString()}`);

        p_tracksUpdateRequired$.next();
        await updatePointsAsync();
      }
      else if (_data.Type === WS_MSG_TYPE_DATA_UPDATED) {
        p_tracksUpdateRequired$.next();
      }
      else if (_data.Type == WS_MSG_PATH_WIPED) {
        const msgData: WsMsgPathWiped = _data.Payload;
        const user = msgData.Username;

        const path = p_paths.get(user);
        path?.setLatLngs([]);

        const geoEntries = p_geoEntries[user];
        if (geoEntries !== undefined)
          geoEntries.length = 0;
      }
      else if (_data.Type == WS_MSG_ROOM_POINTS_UPDATED) {
        console.log("Points were changed, updating markers...");
        await updatePointsAsync();
      }
    });
  }

  if (!p_appCtx.isRoadnikApp) {
    setTimeout(async () => {
      const roomIdIsCorrect = await p_storageApi.isRoomIdValidAsync(p_appCtx.roomId);
      if (!roomIdIsCorrect) {
        console.log(`Incorrect room id: ${p_appCtx.roomId}`);
        Swal.fire({
          icon: "error",
          title: "Room id is missed or invalid",
          text: "Make sure room id is specified and valid",
          footer: `Current room id: ${p_appCtx.roomId}`
        });
      }
    }, 1000);
  }

  setInterval(() => {
    const selectedTrack = p_appCtx.mapState.selectedPath;
    if (selectedTrack !== null)
      updateSelectedPath(selectedTrack, false);
  }, 1000);
}
onStart();

// C# interaction
function sendDataToHost(_msg: JsToCSharpMsg): void {
  try {
    const json = JSON.stringify(_msg);
    (window as any).jsBridge.invokeAction(json);
  }
  catch { }
}

function sendMapStateToRoadnikApp(): void {
  const state = p_appCtx.mapState;
  sendDataToHost({ msgType: HOST_MSG_MAP_STATE, data: state });
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

function setViewToTrack(_pathName: string, _zoom: number): boolean {
  const marker = p_markers.get(_pathName);
  if (marker === undefined)
    return false;

  p_map.flyTo(marker.getLatLng(), _zoom, { duration: 0.5 });
  updateSelectedPath(_pathName);
  return true;
}
(window as any).setViewToTrack = setViewToTrack;

function updateCurrentLocation(_lat: number, _lng: number, _accuracy: number): boolean {
  if (p_appCtx.currentLocationMarker === undefined) {
    const icon = GenerateCircleIcon(10, "black");
    p_appCtx.currentLocationMarker = L.marker([_lat, _lng], { icon: icon, interactive: false });
    console.log("Created current location merker");
  }
  if (p_appCtx.currentLocationCircle === undefined) {
    const circle = L.circle([_lat, _lng], 100, { color: "black", fillColor: '*', fillOpacity: 0.3, interactive: false });
    p_appCtx.currentLocationCircle = circle;
    console.log("Created current location circle");
  }

  p_appCtx.currentLocationMarker
    .setLatLng([_lat, _lng])
    .addTo(p_map);

  p_appCtx.currentLocationCircle
    .setLatLng([_lat, _lng])
    .setRadius(_accuracy)
    .addTo(p_map);

  return true;
}
(window as any).updateCurrentLocation = updateCurrentLocation;
