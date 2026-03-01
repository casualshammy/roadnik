import type { TimedStorageEntry } from "@/data/backend";
import type { AppId } from "@/data/Guid";
import { getCachedColor } from "@/toolkit/mapToolkit";
import L from "leaflet";
import { ref, watch, type Ref } from "vue";

export function setupReachableCircle(
  _map: L.Map,
  _userAppId: Ref<AppId | null>,
  _userTrackPoints: Ref<TimedStorageEntry[] | undefined>
) {
  const reachableCircle = L.circle([0, 0], { radius: 0, color: "black", fillOpacity: 0, interactive: false, dashArray: [8] });

  const cssStyle = `
		width: 100px;
		color: black;
    display: block;
    text-align: left;
    font-size: 14px;
    font-weight: bold;
    margin-top: -20px;
    margin-left: -20px;
		box-shadow: 0 0 0 black;
	`;
  const label = L.marker([0, 0], {
    icon: L.divIcon({
      className: '',
      html: `<span style="${cssStyle}">1 min</span>`,
      iconSize: [0, 0]
    }),
    interactive: false,

  });

  const tickerRef = ref<number>(0);
  setInterval(() => {
    tickerRef.value++;
  }, 1000);

  // Watch for changes in user location and track points
  watch([_userAppId, _userTrackPoints, tickerRef], (_entry) => {
    const [appId, trackPoints] = _entry;
    if (appId === null || trackPoints === undefined) {
      reachableCircle.remove();
      label.remove();
      return;
    }

    const lastLocation = trackPoints.length > 0 ? trackPoints[trackPoints.length - 1] : null;
    if (lastLocation === null) {
      reachableCircle.remove();
      label.remove();
      return;
    }

    const now = Date.now();
    const recentPoints = trackPoints.filter(_ => (now - _.UnixTimeMs) <= 5 * 60 * 1000 * 1000000 && _.Speed !== null); // last 5 minutes
    if (recentPoints.length === 0) {
      reachableCircle.remove();
      label.remove();
      return;
    }

    // calculate avg speed
    const totalSpeed = recentPoints.reduce((_acc, _e) => _acc + _e.Speed!, 0);
    const avgSpeed = totalSpeed / recentPoints.length;
    if (avgSpeed <= 0) {
      reachableCircle.remove();
      label.remove();
      return;
    }

    const centerLatLng = L.latLng(lastLocation.Latitude, lastLocation.Longitude);
    const centerPoint = _map.latLngToLayerPoint(centerLatLng);

    let pxDiff = 0;
    let reachableDistance = 0;
    let reachableMinutes = 0;
    for (let timeMin = 1; timeMin <= 10; timeMin++) {
      reachableMinutes = timeMin;
      reachableDistance = avgSpeed * timeMin * 60;
      const centerEastLatLng = L.latLng(lastLocation.Latitude, centerLatLng.toBounds(reachableDistance * 2).getEast());
      const centerEastPoint = _map.latLngToLayerPoint(centerEastLatLng);
      pxDiff = centerEastPoint.x - centerPoint.x;
      if (pxDiff >= 50)
        break;
    }

    if (pxDiff < 50) {
      reachableCircle.remove();
      label.remove();
      return;
    }

    // update circle
    const color = getCachedColor(appId);
    reachableCircle
      .setStyle({ color: color })
      .setLatLng(centerLatLng)
      .setRadius(reachableDistance)
      .addTo(_map);

    label
      .setLatLng([reachableCircle.getBounds().getNorth(), lastLocation.Longitude])
      .addTo(_map);


    const cssStyle = `
		width: 100px;
		color: ${color};
    display: block;
    text-align: left;
    font-size: 14px;
    font-weight: bold;
    margin-top: -20px;
    margin-left: -20px;
		box-shadow: 0 0 0 ${color};
	`;
    (label.getIcon().options as any).html = `<span style="${cssStyle}">${reachableMinutes} min</span>`;



  });
}