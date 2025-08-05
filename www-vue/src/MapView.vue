<template>
  <LeafletMap
              @created="onMapCreated"
              :location="p_mapLocation"
              :layers="[p_mapsData[p_mapState.layer]]" />

  <SelectedUserPopup
                     v-if="floatingWindowData !== undefined"
                     :state="floatingWindowData"
                     :left="p_mapState.selectedPathWindowLeft"
                     :bottom="p_mapState.selectedPathWindowBottom"
                     @onMoved="onSelectedUserPopupMoved"
                     @onDblClick="onSelectedUserPopupDblClick"
                     @onCloseButton="() => p_mapInteractor.setObservedUser(null)" />

  <ComboBox
            v-if="pathsComboBoxEntries?.length > 0"
            class="paths_combobox"
            :options="pathsComboBoxEntries"
            :value="pathsComboBoxSelectedEntry"
            @changed="onUsersComboBoxChanged">
  </ComboBox>
</template>

<script setup lang="ts">
import { ref, computed, type ComputedRef, type Ref, shallowRef, watch, onMounted, type WatchHandle, onUnmounted, reactive } from "vue";
import L, { type LeafletMouseEvent } from 'leaflet';
import { Subject, switchMap, asyncScheduler, observeOn } from "rxjs";
import Cookies from "js-cookie";
import Swal from "sweetalert2";
import "leaflet-textpath";

import LeafletMap from './components/LeafletMap.vue';
import SelectedUserPopup from './components/SelectedUserPopup.vue';
import ComboBox from './components/ComboBox.vue';
import { type SelectedUserPopupState } from './components/SelectedUserPopup.vue';

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

const p_markers = new Map<string, L.Marker>();
const p_circles = new Map<string, L.Circle>();
const p_paths = reactive(new Map<string, L.Polyline>());
const p_pointMarkers: { [key: number]: L.Marker } = {};
const p_pointMarkersPool = new Pool<L.Marker>(() => L.marker([0, 0]));
const p_gEntries = reactive(new Map<string, TimedStorageEntry[]>());
const p_tracksUpdateRequired$ = new Subject<void>();

const p_map = shallowRef<L.Map>();
const p_backendApi = new BackendApi(p_appCtx.apiUrl);
const p_hostApi = new HostApi(p_appCtx);
const p_mapInteractor = new MapInteractor(p_appCtx, p_hostApi, p_map, p_paths, p_gEntries);

const pathsComboBoxEntries: ComputedRef<string[]> = computed(() => {
  const array: string[] = [];

  p_paths.forEach((_value, _key) => {
    array.push(_key);
  });

  array.sort((_a, _b) => _a > _b ? -1 : 1);
  array.unshift('-- Paths --');

  return array;
});

const floatingWindowData: ComputedRef<SelectedUserPopupState | undefined> = computed(() => {
  const user = p_mapState.value.selectedPath;
  if (user === null)
    return undefined;

  const entries = p_gEntries.get(user);
  if (entries === undefined || entries.length === 0)
    return undefined;

  const lastEntry = entries[entries.length - 1];
  const data: SelectedUserPopupState = {
    user: user,
    timestamp: lastEntry.UnixTimeMs,
    battery: lastEntry.Battery ?? undefined,
    gsmSignal: lastEntry.GsmSignal ?? undefined,
    speed: (lastEntry.Speed ?? 0) * 3.6,
    altitude: lastEntry.Altitude,
    accuracy: lastEntry.Accuracy ?? undefined,
    color: p_appCtx.userColors.get(user) ?? 'black',
    hr: lastEntry.HR ?? undefined
  };

  return data;
});

const pathsComboBoxSelectedEntry: Ref<string | undefined> = ref();

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
  _map.on('zoomend', () => {
    const location = _map.getCenter();

    p_mapState.value.lat = location.lat;
    p_mapState.value.lng = location.lng;
    p_mapState.value.zoom = _map.getZoom();

    if (!p_appCtx.isRoadnikApp) {
      const stateString = `${p_mapState.value.lat}:${p_mapState.value.lng}:${p_mapState.value.zoom}`;
      Cookies.set(Consts.COOKIE_MAP_STATE, stateString);
    }
    else {
      p_hostApi.sendMapStateToRoadnikApp();
    }
  });
  _map.on('moveend', () => {
    const location = _map.getCenter();

    p_mapState.value.lat = location.lat;
    p_mapState.value.lng = location.lng;
    p_mapState.value.zoom = _map.getZoom();

    if (!p_appCtx.isRoadnikApp) {
      const stateString = `${p_mapState.value.lat}:${p_mapState.value.lng}:${p_mapState.value.zoom}`;
      Cookies.set(Consts.COOKIE_MAP_STATE, stateString);
    }
    else {
      p_hostApi.sendMapStateToRoadnikApp();
    }
  });
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

  if (p_appCtx.roomId !== null) {
    p_backendApi.setupWs(p_appCtx.roomId, async (_ws, _data) => {
      console.log(`WS MSG: ${_data.Type}`);
      if (_data.Type === Consts.WS_MSG_TYPE_HELLO) {
        const msgData: WsMsgHello = _data.Payload;
        p_appCtx.maxTrackPoints = msgData.MaxPathPointsPerRoom;
        console.log(`Max saved points: ${p_appCtx.maxTrackPoints}`);
        console.log(`Server time: ${new Date(msgData.UnixTimeMs).toISOString()}`);

        p_tracksUpdateRequired$.next();
        await updatePointsAsync();
      }
      else if (_data.Type === Consts.WS_MSG_TYPE_DATA_UPDATED) {
        p_tracksUpdateRequired$.next();
      }
      else if (_data.Type == Consts.WS_MSG_PATH_WIPED) {
        const msgData: WsMsgPathWiped = _data.Payload;
        const user = msgData.Username;

        const path = p_paths.get(user);
        path?.setLatLngs([]);

        const geoEntries = p_gEntries.get(user);
        if (geoEntries !== undefined)
          geoEntries.length = 0;
      }
      else if (_data.Type == Consts.WS_MSG_ROOM_POINTS_UPDATED) {
        console.log("Points were changed, updating markers...");
        await updatePointsAsync();
      }
      else if (_data.Type == Consts.WS_MSG_PATH_TRUNCATED) {
        const msgData: WsMsgPathTruncated = _data.Payload;

        const geoEntries = p_gEntries.get(msgData.Username);
        if (geoEntries !== undefined) {
          const entriesToDelete = geoEntries.length - msgData.PathPoints;
          if (entriesToDelete > 0) {
            geoEntries.splice(0, entriesToDelete);

            const path = p_paths.get(msgData.Username);
            if (path !== undefined) {
              const points = geoEntries.map(_x => new L.LatLng(_x.Latitude, _x.Longitude, _x.Altitude));
              path.setLatLngs(points);
            }
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
        Swal.fire({
          icon: "error",
          title: "Room id is missed or invalid",
          text: "Make sure room id is specified and valid",
          footer: `Current room id: ${p_appCtx.roomId}`
        });
      }
    }, 1000);

    if ("geolocation" in navigator) {
      const options = {
        enableHighAccuracy: true,
        maximumAge: 3000,
        timeout: 30000,
      };

      const onUpdate = (_pos: GeolocationPosition) => {
        p_mapInteractor.setLocationAndHeading(_pos.coords.latitude, _pos.coords.longitude, _pos.coords.accuracy, null);
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
    const selectedPath = p_mapState.value.selectedPath;
    if (selectedPath === null)
      return;

    const geoEntries = p_gEntries.get(selectedPath);
    const lastLocation = geoEntries !== undefined ? geoEntries[geoEntries.length - 1] : undefined;
    if (lastLocation === undefined)
      return;

    _map.flyTo([lastLocation.Latitude, lastLocation.Longitude]);
    console.log(`Map is fly to the latest location of user ${selectedPath}`);
  }, false);
}

function onSelectedUserPopupMoved(_left: number, _bottom: number) {
  p_mapState.value.selectedPathWindowLeft = _left;
  p_mapState.value.selectedPathWindowBottom = _bottom;

  if (!p_appCtx.isRoadnikApp) {
    Cookies.set(Consts.COOKIE_SELECTED_PATH_LEFT, _left.toString());
    Cookies.set(Consts.COOKIE_SELECTED_PATH_BOTTOM, _bottom.toString());
  }
  else {
    p_hostApi.sendMapStateToRoadnikApp();
  }
}

function onSelectedUserPopupDblClick() {
  const path = p_mapState.value.selectedPath;
  if (path !== null)
    p_mapInteractor.setMapCenterToUser(path);
}

function onUsersComboBoxChanged(_value: string) {
  const user = p_paths.get(_value) !== undefined ? _value : null;
  p_mapInteractor.setObservedUser(user, true);

  if (user !== null)
    p_mapInteractor.setMapCenterToUser(user);
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

  if (data === null)
    return;

  const prevOffset = p_appCtx.lastTracksOffset;
  p_appCtx.lastTracksOffset = data.LastUpdateUnixMs;
  console.log(`New last offset: ${p_appCtx.lastTracksOffset}; points to process: ${data.Entries.length}`);

  const usersMap = CommonToolkit.groupBy(data.Entries, _ => _.Username);
  const users = Object.keys(usersMap);

  // init users controls
  for (const user of users)
    initControlsForUser(user);

  // update users controls
  for (const user of users) {
    const userData = usersMap[user];
    updateControlsForUser(user, userData, prevOffset === 0);
  }

  document.title = `Roadnik: ${p_appCtx.roomId} (${p_paths.size})`;
  if (data.MoreEntriesAvailable) {
    p_tracksUpdateRequired$.next();
    return;
  }

  if (!p_appCtx.firstTracksSyncCompleted) {
    const selectedPath = p_mapState.value.selectedPath;
    if (selectedPath === null) {
      console.log("Initial selected path is not set, setting default view...");
    }
    else if (!p_mapInteractor.setMapCenterToUser(selectedPath, p_map.value!.getZoom())) {
      console.log("Initial selected path is set but not found, setting view to all paths...");
      p_mapInteractor.setMapCenterToAllUsers();
    }
    else {
      console.log(`Initial selected path is ${selectedPath}`);
      p_mapInteractor.setObservedUser(selectedPath);
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

function initControlsForUser(_user: string): void {
  let color = p_appCtx.userColors.get(_user);
  if (color === undefined) {
    color = CommonToolkit.getColorForString(_user); //TRACK_COLORS[p_appCtx.userColorIndex++ % TRACK_COLORS.length];
    p_appCtx.userColors.set(_user, color);
    console.log(`Color for user ${_user}: ${color}`);
  }

  if (p_markers.get(_user) === undefined) {
    const icon = MapToolkit.GeneratePulsatingCircleIcon(15, color);
    const marker = L.marker([51.4768, 0.0006], { title: _user, icon: icon })
      .addTo(p_map.value!)
      .addEventListener('click', () => {
        p_mapInteractor.setObservedUser(_user);
      });

    p_markers.set(_user, marker);
  }
  if (p_circles.get(_user) === undefined)
    p_circles.set(_user, L.circle([51.4768, 0.0006], { radius: 100, color: color, fillColor: '*', fillOpacity: 0.3 })
      .addTo(p_map.value!));
  if (p_paths.get(_user) === undefined) {
    const path = L.polyline([], { color: color, smoothFactor: 1, weight: 6 })
      .addTo(p_map.value!)
      .bindPopup("")
      .addEventListener("click", (_ev: LeafletMouseEvent) => {
        const entries = p_gEntries.get(_user);
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
          const popupText = buildPathPointPopup(_user, nearestEntry);
          path.setPopupContent(popupText);
          path.openPopup(nearestLatLng);
        }
      });

    (path as any).setText('  ➤  ', {
      repeat: true,
      offset: 11,
      below: true,
      bold: true,
      attributes: {
        fill: color,
        'font-size': '30',
        'font-family': 'monospace',
        'font-weight': 'bold'
      }
    });

    p_paths.set(_user, path);
  }

  if (!p_gEntries.has(_user))
    p_gEntries.set(_user, []);
}

function updateControlsForUser(
  _user: string,
  _entries: TimedStorageEntry[],
  _isFirstDataChunk: boolean
): void {
  if (_entries.length === 0)
    return;

  const path = p_paths.get(_user);
  if (path === undefined) {
    console.error(`Error occured while trying to update path of user '${_user}': leaflet's polyline is undefined`);
    return;
  }

  const geoEntries = p_gEntries.get(_user);
  if (geoEntries === undefined) {
    console.error(`Error occured while trying to update path of user '${_user}': path entries array is undefined`);
    return;
  }

  const sortedEntries = _entries.sort((_a, _b) => _a.UnixTimeMs - _b.UnixTimeMs);
  const lastEntry = sortedEntries[sortedEntries.length - 1];

  geoEntries.push(...sortedEntries);
  const geoEntriesExcessiveCount = geoEntries.length - p_appCtx.maxTrackPoints;
  if (geoEntriesExcessiveCount > 0) {
    const removedEntries = geoEntries.splice(0, geoEntriesExcessiveCount);
    console.log(`${removedEntries.length} geo entries were removed for user ${_user}`);
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

  if (p_mapState.value.selectedPath === _user) {
    // p_mapInteractor.setObservedUser(_user); // why?
    if (document.hasFocus()) // if we fly to location in background, path position will be uncorrect until next location update
      p_mapInteractor.setMapCenter(lastLocation.lat, lastLocation.lng, p_map.value!.getZoom(), 500);
  }

  const points = geoEntries.map(_ => new L.LatLng(_.Latitude, _.Longitude, _.Altitude));
  path.setLatLngs(points);
  console.log(`Path '${_user}' now contains ${points.length} points`);
}

function buildPathPointPopup(_user: string, _entry: TimedStorageEntry): string {
  const kmh = (_entry.Speed ?? 0) * 3.6;

  const elapsedSinceLastUpdate = TimeSpan.fromMilliseconds(Date.now() - _entry.UnixTimeMs);
  let elapsedString = "now";
  if (Math.abs(elapsedSinceLastUpdate.totalSeconds) > 5)
    elapsedString = `${elapsedSinceLastUpdate.toString(false)} ago`;

  const popUpText =
    `<center>
            <b>${_user}</b> (${elapsedString})
            </br>
            🔋${((_entry.Battery ?? 0) * 100).toFixed(0)}% 📶${((_entry.GsmSignal ?? 0) * 100).toFixed(0)}%
        </center>
        <p style="margin-bottom: 0px">
        🚀${kmh.toFixed(1)} km/h ⛰${Math.ceil(_entry.Altitude)} m 📡${Math.ceil(_entry.Accuracy ?? 100)} m
        </p>`;

  return popUpText;
}

let unwatchSelectedUser: WatchHandle;
onMounted(() => {
  unwatchSelectedUser = watch(computed(() => p_mapState.value.selectedPath), _newSelectedUser => {
    pathsComboBoxSelectedEntry.value = _newSelectedUser ?? undefined;
  }, { immediate: true });
});

onUnmounted(() => {
  unwatchSelectedUser();
});

</script>

<style scoped>
.paths_combobox {
  position: fixed;
  z-index: 10000;
  left: 5px;
  bottom: 5px;
}
</style>
