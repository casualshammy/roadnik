<template>
  <LeafletMap
              @created="onMapCreated"
              :location="p_mapLocation"
              :layers="[p_mapsData[p_mapState.layer]]" />

  <UserStatusBar
                 v-if="p_appIds.size > 0"
                 :appIds="p_appIds"
                 :gEntries="p_gEntries"
                 :selectedAppId="p_mapState.selectedAppId"
                 @select="onUserStatusBarSelect"
                 @deselect="() => p_mapInteractor.setObservedUser(null)"
                 @centerOnUser="_appId => p_mapInteractor.setMapCenterToUser(_appId)" />
</template>

<script setup lang="ts">
import { ref, computed, shallowRef, reactive } from "vue";
import L, { type LeafletMouseEvent } from 'leaflet';
import { Subject, switchMap, asyncScheduler, observeOn } from "rxjs";
import Cookies from "js-cookie";
import { DialogAlertError } from 'v-dialogs'

import LeafletMap from './components/LeafletMap.vue';
import UserStatusBar from './components/UserStatusBar.vue';

import { type LatLngZoom } from './data/LatLngZoom';
import * as MapToolkit from './toolkit/mapToolkit';
import { BackendApi } from './api/backendApi';
import { HostApi } from './api/hostApi';
import { CreateAppCtx, GetApiUrl } from './data/AppCtx';
import type { TimedStorageEntry, GetPathResData, WsMsgHello, WsMsgPathWiped, WsMsgPathTruncated } from '@/data/backend';
import * as Consts from './data/Consts';
import * as CommonToolkit from './toolkit/commonToolkit';
import { TimeSpan } from './toolkit/timespan';
import { Pool } from './toolkit/Pool';
import { MapInteractor } from "./parts/mapInteractor";
import { getHeartRateString } from "./toolkit/commonToolkit";
import { getCachedColor } from "./toolkit/mapToolkit";
import type { AppId } from "./data/Guid";

const apiUrl = GetApiUrl();
const p_mapsData = MapToolkit.GetMapLayers(apiUrl);
const p_mapOverlays = MapToolkit.GetMapOverlayLayers(apiUrl);
const p_appCtx = CreateAppCtx(apiUrl, p_mapsData, p_mapOverlays);
const p_mapState = computed(() => p_appCtx.mapState.value);
const p_mapLocation = ref<LatLngZoom>({
  lat: p_mapState.value.lat,
  lng: p_mapState.value.lng,
  zoom: p_mapState.value.zoom
});

const p_appIds = reactive(new Map<AppId, string>());
const p_markers = new Map<AppId, L.Marker>();
const p_circles = new Map<AppId, L.Circle>();
const p_paths = new Map<AppId, L.Polyline>();
const p_pathArrows = new Map<string, L.Marker[]>();
const p_pointMarkers: { [key: number]: L.Marker } = {};
const p_pointMarkersPool = new Pool<L.Marker>(() => L.marker([0, 0]));
const p_gEntries = reactive(new Map<AppId, TimedStorageEntry[]>());
const p_tracksUpdateRequired$ = new Subject<void>();
const p_userPathUpdated$ = new Subject<AppId[]>();

const p_map = shallowRef<L.Map>();
const p_backendApi = new BackendApi(p_appCtx.apiUrl);
const p_hostApi = new HostApi(p_appCtx);
const p_mapInteractor = new MapInteractor(p_appCtx, p_hostApi, p_map, p_paths, p_gEntries);

document.title = `Roadnik: ${p_appCtx.roomId}`;

function onMapCreated(_map: L.Map) {
  p_map.value = _map;

  setupMap(_map);
  setupDataFlow(_map);
}

function setupMap(_map: L.Map) {
  for (const overlay of p_mapState.value.overlays)
    _map.addLayer(p_mapOverlays[overlay]);

  _map.attributionControl.setPrefix(false);
  if (p_appCtx.isRoadnikApp)
    _map.attributionControl.remove();

  _map.on('baselayerchange', function (_e) {
    p_mapState.value.layer = _e.name;

    console.log(`Layer changed to ${p_mapState.value.layer}`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(Consts.COOKIE_MAP_LAYER, p_mapState.value.layer);
    else
      p_hostApi.sendMapStateToRoadnikApp();
  });

  _map.on('overlayadd', function (_e) {
    const overlay = _e.name;
    if (!p_mapState.value.overlays.includes(overlay))
      p_mapState.value.overlays.push(overlay);

    console.log(`Added overlay '${overlay}'`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(Consts.COOKIE_MAP_OVERLAY, JSON.stringify(p_mapState.value.overlays));
    else
      p_hostApi.sendMapStateToRoadnikApp();
  });

  _map.on('overlayremove', function (_e) {
    const overlay = _e.name;
    p_mapState.value.overlays = p_mapState.value.overlays.filter(_v => _v !== overlay);

    console.log(`Removed overlay '${overlay}'`);

    if (!p_appCtx.isRoadnikApp)
      Cookies.set(Consts.COOKIE_MAP_OVERLAY, JSON.stringify(p_mapState.value.overlays));
    else
      p_hostApi.sendMapStateToRoadnikApp();
  });

  function onMapMoveOrZoom() {
    const location = _map.getCenter();

    p_mapState.value.lat = location.lat;
    p_mapState.value.lng = location.lng;
    p_mapState.value.zoom = _map.getZoom();

    for (const appId of p_appIds.keys()) {
      const geoEntries = p_gEntries.get(appId);
      if (geoEntries !== undefined)
        updatePathArrows(appId, geoEntries);
    }

    if (!p_appCtx.isRoadnikApp) {
      const stateString = `${p_mapState.value.lat}:${p_mapState.value.lng}:${p_mapState.value.zoom}`;
      Cookies.set(Consts.COOKIE_MAP_STATE, stateString);
    }
    else {
      p_hostApi.sendMapStateToRoadnikApp();
    }
  }

  _map.on('zoomend', onMapMoveOrZoom);
  _map.on('moveend', onMapMoveOrZoom);

  _map.on("contextmenu", function (_e) {
    if (p_appCtx.roomId === null)
      return;

    console.log(`Initializing waypoint in ${_e.latlng}...`);
    if (p_appCtx.isRoadnikApp) {
      p_hostApi.sendWaypointAddStarted(_e.latlng);
    }
    else {
      const msg = prompt("Please enter a description for point:");
      if (msg !== null)
        p_backendApi.createPointAsync(p_appCtx.roomId, "", _e.latlng, msg);
    }
  });

  _map.on('dragstart', (event) => {
    if (p_appCtx.isRoadnikApp) {
      p_hostApi.sendMapDragStarted();
    }
  });

  L.control.scale({
    position: 'bottomright',
    maxWidth: 200,
    metric: true,
    imperial: false,
    updateWhenIdle: true
  }).addTo(_map);

  L.control.layers(
    p_mapsData, p_mapOverlays
  ).addTo(_map);

  L.control.zoom({
    position: 'topright'
  }).addTo(_map);
}

function setupDataFlow(_map: L.Map) {
  p_tracksUpdateRequired$
    .pipe(
      observeOn(asyncScheduler),
      switchMap(async () => await updatePathsAsync()))
    .subscribe();

  p_userPathUpdated$
    .pipe(
      observeOn(asyncScheduler))
    .subscribe(_appIds => {
      for (const appId of _appIds) {
        const entries = p_gEntries.get(appId) ?? [];
        const username = p_appIds.get(appId);
        if (username === undefined) {
          console.error(`Error occured while trying to update controls for user '${appId}': username is undefined`);
          return;
        }

        initControlsForUser(appId, username);
        updateControlsForUser(appId, entries);
        updatePathArrows(appId, entries);
      }
    });

  if (p_appCtx.roomId !== null) {
    p_backendApi.setupEventSource(p_appCtx.roomId, {
      [Consts.WS_MSG_TYPE_HELLO]: async _ev => {
        console.log(`Received SSE '${Consts.WS_MSG_TYPE_HELLO}' message from server`);
        const msgData: WsMsgHello = JSON.parse(_ev.data);
        p_appCtx.maxTrackPoints = msgData.MaxPathPointsPerRoom;
        console.log(`Max saved points: ${p_appCtx.maxTrackPoints}`);
        console.log(`Server time: ${new Date(msgData.UnixTimeMs).toISOString()}`);

        for (const appId of [...p_gEntries.keys()]) {
          if (!(appId in msgData.Timestamps)) {
            removeUserPath(appId);
            console.log(`Removed user '${appId}' as server indicates that it's not exist anymore`);
            continue;
          }

          const oldestTimestamp = msgData.Timestamps[appId];
          const geoEntries = p_gEntries.get(appId);
          if (geoEntries === undefined)
            continue;

          const oldEntries = geoEntries.filter(_ => _.UnixTimeMs < oldestTimestamp);
          if (oldEntries.length === 0)
            continue;

          geoEntries.splice(0, oldEntries.length);

          const username = p_appIds.get(appId) ?? "unknown";
          console.log(`Removed ${oldEntries.length} old entries for user '${appId}/${username}' due to server's indication that they are not exist anymore`);
        }

        p_tracksUpdateRequired$.next();
        await updatePointsAsync();
      },
      [Consts.WS_MSG_TYPE_DATA_UPDATED]: () => {
        console.log(`Received SSE '${Consts.WS_MSG_TYPE_DATA_UPDATED}' message from server`);
        p_tracksUpdateRequired$.next();
      },
      [Consts.WS_MSG_PATH_WIPED]: _ev => {
        console.log(`Received SSE '${Consts.WS_MSG_PATH_WIPED}' message from server`);
        const msgData: WsMsgPathWiped = JSON.parse(_ev.data);
        const username = p_appIds.get(msgData.AppId) ?? "unknown";
        if (p_gEntries.has(msgData.AppId)) {
          removeUserPath(msgData.AppId);
          console.log(`Wiped path of user '${msgData.AppId}/${username}' as server indicates that it's wiped`);
        }
      },
      [Consts.WS_MSG_ROOM_POINTS_UPDATED]: async () => {
        console.log(`Received SSE '${Consts.WS_MSG_ROOM_POINTS_UPDATED}' message from server`);
        await updatePointsAsync();
      },
      [Consts.WS_MSG_PATH_TRUNCATED]: _ev => {
        console.log(`Received SSE '${Consts.WS_MSG_PATH_TRUNCATED}' message from server`);
        const msgData: WsMsgPathTruncated = JSON.parse(_ev.data);
        const geoEntries = p_gEntries.get(msgData.AppId);
        if (geoEntries !== undefined && geoEntries.length > 0) {
          const entriesToDelete = geoEntries.length - msgData.PathPoints;
          if (entriesToDelete > 0) {
            geoEntries.splice(0, entriesToDelete);
            p_userPathUpdated$.next([msgData.AppId]);
            const username = p_appIds.get(msgData.AppId) ?? "unknown";
            console.log(`Truncated path of user '${msgData.AppId}/${username}' by removing ${entriesToDelete} old entries as server indicates that they are not exist anymore`);
          }
        }
      }
    });
  }

  if (!p_appCtx.isRoadnikApp) {
    setTimeout(async () => {
      const roomIdIsCorrect = await p_backendApi.isRoomIdValidAsync(p_appCtx.roomId);
      if (!roomIdIsCorrect) {
        console.log(`Incorrect room id: ${p_appCtx.roomId}`);
        DialogAlertError(
          `Make sure room id is specified and valid.\nCurrent room id: ${p_appCtx.roomId}`,
          undefined,
          {
            header: true,
            title: "Room id is missed or invalid123",
            messageType: 'error',
            icon: true,
            colorfulShadow: true
          }
        );
      }
    }, 1000);

    if ("geolocation" in navigator) {
      const options = {
        enableHighAccuracy: true,
        maximumAge: 3000,
        timeout: 30000,
      };

      const onUpdate = (_pos: GeolocationPosition) => {
        p_mapInteractor.setLocationAndHeading(_pos.coords.latitude, _pos.coords.longitude, _pos.coords.accuracy, _pos.coords.heading, _pos.coords.speed);
        p_mapInteractor.setCompassHeading(_pos.coords.heading);
      };
      navigator.geolocation.watchPosition(onUpdate, undefined, options);
      console.log(`Subscribed to geolocation updates`);
    } else {
      console.log(`Geolocation is not available`);
    }
  }

  window.addEventListener("focus", () => {
    // fly to selected user's position
    const selectedAppId = p_mapState.value.selectedAppId;
    if (selectedAppId === null)
      return;

    const geoEntries = p_gEntries.get(selectedAppId);
    const lastLocation = geoEntries !== undefined ? geoEntries[geoEntries.length - 1] : undefined;
    if (lastLocation === undefined)
      return;

    _map.flyTo([lastLocation.Latitude, lastLocation.Longitude]);
    console.log(`Map is fly to the latest location of user '${selectedAppId}/${lastLocation.Username}'`);
  }, false);
}

function onUserStatusBarSelect(_appId: AppId) {
  p_mapInteractor.setObservedUser(_appId);
  p_mapInteractor.setMapCenterToUser(_appId);
}

async function updatePathsAsync() {
  if (p_appCtx.roomId === null)
    return;

  let data: GetPathResData;
  try {
    data = await p_backendApi.getPathsAsync(p_appCtx.roomId, p_appCtx.lastTracksOffset);
  } catch (error) {
    console.warn(`Got error trying to fetch paths data with offset ${p_appCtx.lastTracksOffset}, retrying...\n${error}`);
    await CommonToolkit.sleepAsync(1000);
    p_tracksUpdateRequired$.next();
    return;
  }

  p_appCtx.lastTracksOffset = data.LastUpdateUnixMs;
  console.log(`New last offset: ${p_appCtx.lastTracksOffset}; points to process: ${data.Entries.length}`);

  const appIdWithChanges: AppId[] = updateData(data.Entries);
  p_userPathUpdated$.next(appIdWithChanges);

  if (data.MoreEntriesAvailable) {
    p_tracksUpdateRequired$.next();
    return;
  }

  if (!p_appCtx.firstTracksSyncCompleted) {
    const selectedAppId = p_mapState.value.selectedAppId;
    if (selectedAppId === null) {
      console.log("Initial selected app id is not set, setting default view...");
    }
    else if (!p_mapInteractor.setMapCenterToUser(selectedAppId, p_map.value!.getZoom())) {
      console.log("Initial selected app id is set but not found, setting view to all paths...");
      p_mapInteractor.setMapCenterToAllUsers();
    }
    else {
      console.log(`Initial selected path is ${selectedAppId}`);
      p_mapInteractor.setObservedUser(selectedAppId);
    }

    if (p_appCtx.isRoadnikApp)
      p_hostApi.sendTracksSynchronized(true);

    p_appCtx.firstTracksSyncCompleted = true;
  }
  else {
    if (p_appCtx.isRoadnikApp)
      p_hostApi.sendTracksSynchronized(false);
  }
}

/**
 * Updates p_gEntries and p_appIds
 */
function updateData(_newEntries: TimedStorageEntry[]): AppId[] {
  const userAppsMap = CommonToolkit.groupBy(_newEntries, _ => _.AppId);

  const result: AppId[] = [];
  const entryPairs = Object.entries(userAppsMap);
  for (const [appId, userData] of entryPairs) {
    const userName = userData[0].Username;

    if (!p_appIds.has(appId)) {
      p_appIds.set(appId, userName);
      console.log(`New user detected: '${appId}/${userName}'`);
    }

    let geoEntries = p_gEntries.get(appId);
    if (geoEntries === undefined) {
      geoEntries = [];
      p_gEntries.set(appId, geoEntries);
    }

    geoEntries.push(...userData);
    geoEntries.sort((_a, _b) => _a.UnixTimeMs - _b.UnixTimeMs);

    const geoEntriesExcessiveCount = geoEntries.length - p_appCtx.maxTrackPoints;
    if (geoEntriesExcessiveCount > 0) {
      const removedEntries = geoEntries.splice(0, geoEntriesExcessiveCount);
      console.log(`${removedEntries.length} geo entries were removed for user '${appId}/${userName}'`);
    }

    result.push(appId);
  }

  return result;
}

async function updatePointsAsync() {
  if (p_appCtx.roomId === null)
    return;

  const data = await p_backendApi.listPointsAsync(p_appCtx.roomId);
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
      marker.addTo(p_map.value!);
      marker.bindPopup(text);
      marker.on("contextmenu", () => {
        p_backendApi.deletePointAsync(p_appCtx.roomId!, entry.PointId);
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

function initControlsForUser(
  _appId: string,
  _username: string
): void {
  if (p_markers.has(_appId) && p_circles.has(_appId) && p_paths.has(_appId))
    return;

  const map = p_map.value;
  if (map === undefined) {
    console.error(`Error occured while trying to init controls for user '${_appId}/${_username}': map is undefined`);
    return;
  }

  const color = getCachedColor(_appId);
  console.log(`Color for user ${_appId}/${_username}: ${color}`);

  if (p_markers.get(_appId) === undefined) {
    const icon = MapToolkit.GeneratePulsatingCircleIcon(15, color);
    const marker = L.marker([51.4768, 0.0006], { title: _username, icon: icon })
      .addTo(map)
      .addEventListener('click', () => {
        p_mapInteractor.setObservedUser(_appId);
      });

    p_markers.set(_appId, marker);
  }

  if (p_circles.get(_appId) === undefined)
    p_circles.set(_appId, L.circle([51.4768, 0.0006], { radius: 100, color: color, fillColor: '*', fillOpacity: 0.3 })
      .addTo(map));

  if (p_paths.get(_appId) === undefined) {
    const path = L.polyline([], { color: color, smoothFactor: 1, weight: 6, renderer: MapToolkit.TOLERANT_RENDERER })
      .addTo(map)
      .bindPopup("")
      .addEventListener("click", (_ev: LeafletMouseEvent) => {
        const entries = p_gEntries.get(_appId);
        if (entries === undefined)
          return;

        let nearestLatLng: L.LatLng | undefined = undefined;
        let nearestEntry: TimedStorageEntry | undefined = undefined;
        for (let entry of entries) {
          const latLng = new L.LatLng(entry.Latitude, entry.Longitude, entry.Altitude);
          if (nearestLatLng === undefined || _ev.latlng.distanceTo(latLng) < _ev.latlng.distanceTo(nearestLatLng)) {
            nearestLatLng = latLng;
            nearestEntry = entry;
          }
        }

        if (nearestLatLng !== undefined && nearestEntry !== undefined) {
          const popupText = buildPathPointPopup(nearestEntry);
          path.setPopupContent(popupText);
          path.openPopup(nearestLatLng);
        }
      });

    p_paths.set(_appId, path);
  }
}

function updateControlsForUser(
  _appId: string,
  _entries: TimedStorageEntry[]
): void {
  const username = p_appIds.get(_appId) ?? "unknown";

  const map = p_map.value;
  if (map === undefined) {
    console.error(`Error occured while trying to update path of user '${_appId}/${username}': map is undefined`);
    return;
  }

  const path = p_paths.get(_appId);
  if (path === undefined) {
    console.error(`Error occured while trying to update path of user '${_appId}/${username}': leaflet's polyline is undefined`);
    return;
  }

  const circle = p_circles.get(_appId);
  if (circle === undefined) {
    console.error(`Error occured while trying to update path of user '${_appId}/${username}': leaflet's circle is undefined`);
    return;
  }

  const marker = p_markers.get(_appId);
  if (marker === undefined) {
    console.error(`Error occured while trying to update path of user '${_appId}/${username}': leaflet's marker is undefined`);
    return;
  }

  if (_entries.length === 0) {
    circle.remove();
    marker.remove();
    path.setLatLngs([]);
    return;
  }

  const lastEntry = _entries[_entries.length - 1];
  const lastLocation = new L.LatLng(lastEntry.Latitude, lastEntry.Longitude, lastEntry.Altitude);

  circle.setLatLng(lastLocation);
  circle.setRadius(lastEntry.Accuracy ?? 100);
  circle.addTo(map);
  circle.bringToFront();

  marker.setLatLng(lastLocation);
  marker.addTo(map);

  if (p_mapState.value.selectedAppId === _appId) {
    if (document.hasFocus()) // if we fly to location in background, path position will be uncorrect until next location update
      p_mapInteractor.setMapCenter(lastLocation.lat, lastLocation.lng, map.getZoom(), 500);
  }

  const points = _entries.map(_ => new L.LatLng(_.Latitude, _.Longitude, _.Altitude));
  path.setLatLngs(points);
  console.log(`Path '${_appId}/${username}' now contains ${points.length} points`);
}

function updatePathArrows(
  _appId: string,
  _entries: TimedStorageEntry[]
): void {
  const username = p_appIds.get(_appId) ?? "unknown";

  const map = p_map.value;
  if (!map) {
    console.error(`Error occured while trying to update path arrows of user '${_appId}/${username}': map is undefined`);
    return;
  }

  const existingArrows = p_pathArrows.get(_appId) ?? [];

  // If not enough points, remove any existing arrows and exit
  if (_entries.length < 2) {
    for (const m of existingArrows)
      m.remove();

    return;
  }

  const mapBounds = map.getBounds();
  const minPixelSpacing = 100; // px between arrows

  // Start from the first point, compare with subsequent points
  let prevGeoPoint = _entries[0];
  let prevScreenPt = map.latLngToContainerPoint([prevGeoPoint.Latitude, prevGeoPoint.Longitude]);
  let arrowsIndex = -1;
  let newMarkersCounter = 0;

  for (let i = 1; i < _entries.length - 1; i++) {
    const entry = _entries[i];
    const entryLatLng: L.LatLngExpression = [entry.Latitude, entry.Longitude];
    const screenPt = map.latLngToContainerPoint(entryLatLng);
    const isInView = mapBounds.contains(entryLatLng);

    if (isInView && screenPt.distanceTo(prevScreenPt) >= minPixelSpacing) {
      const bearing = MapToolkit.initialBearing(prevGeoPoint.Latitude, prevGeoPoint.Longitude, entry.Latitude, entry.Longitude);

      let marker = existingArrows[++arrowsIndex];
      if (marker === undefined) {
        ++newMarkersCounter;
        marker = L.marker([0, 0], { draggable: false, interactive: false, keyboard: false })
          .setRotationOrigin("center")
          .setIcon(MapToolkit.getCachedArrowIcon(getCachedColor(_appId + "_arrow_color"))); // we want arrows to be different color
      }

      marker
        .setLatLng(entryLatLng)
        .setRotationAngle(bearing - 90)
        .addTo(map);

      existingArrows[arrowsIndex] = marker;

      prevScreenPt = screenPt;
    }

    prevGeoPoint = entry;
  }

  // Remove any excess markers that are no longer needed
  for (let i = arrowsIndex + 1; i < existingArrows.length; i++)
    existingArrows[i].remove();

  p_pathArrows.set(_appId, existingArrows);
  console.log(`Total arrow markers for user '${_appId}/${username}'': ${arrowsIndex + 1}/${existingArrows.length} (new: ${newMarkersCounter})`);
}

function buildPathPointPopup(_entry: TimedStorageEntry): string {
  const kmh = (_entry.Speed ?? 0) * 3.6;

  const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - _entry.UnixTimeMs);
  let elapsedString = "now";
  if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
    elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

  const hrData = getHeartRateString(_entry.HR);

  const popUpText =
    `<center>
      <b>${_entry.Username}</b> (${elapsedString})
      </br>
      🔋${((_entry.Battery ?? 0) * 100).toFixed(0)}% 📶${((_entry.GsmSignal ?? 0) * 100).toFixed(0)}% ${hrData ?? ""}
    </center>
    <p style="margin-bottom: 0px">
      🚀${kmh.toFixed(1)} km/h ⛰${Math.ceil(_entry.Altitude)} m 📡${Math.ceil(_entry.Accuracy ?? 100)} m
    </p>`;

  return popUpText;
}

function removeUserPath(
  _appId: AppId
): void {
  p_circles.get(_appId)?.remove();
  p_markers.get(_appId)?.remove();
  p_paths.get(_appId)?.setLatLngs([]);
  p_pathArrows.get(_appId)?.forEach(a => a.remove());

  p_gEntries.delete(_appId);
  p_appIds.delete(_appId);
}

</script>

<style scoped>
</style>
