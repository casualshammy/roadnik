import { GenerateCircleIcon } from "../toolkit/mapToolkit";
import L, { LatLngBounds, type LatLngExpression } from "leaflet";
import 'leaflet-rotatedmarker';

const SPEED_THRESHOLD_MS = 0.55; // m/s (~2 km/h)
const REACH_VIEWPORT_SHORT = 1 / 6; // fraction of viewport height for the small circle
const REACH_TIME_STEPS_S = [1, 2, 5, 10, 20, 30, 60].map(m => m * 60); // allowed time values in seconds

export class CurrentLocationControl {
  private static readonly p_reachCircleMarkerCache: Map<string, L.DivIcon> = new Map();
  private readonly p_map: L.Map;
  private readonly p_marker: L.Marker;
  private readonly p_accuracyCircle: L.Circle;
  private readonly p_fixedCircle: L.CircleMarker;
  private readonly p_fixedCircleBg: L.CircleMarker;
  private readonly p_directionLine: L.Polyline;
  private readonly p_headingMarker: L.Marker;
  private readonly p_reachCircleShort: L.Circle;
  private readonly p_reachCircleLong: L.Circle;
  private readonly p_reachLabelShort: L.Marker;
  private readonly p_reachLabelLong: L.Marker;
  private readonly p_speedSamples: { time: number; speed: number }[] = [];

  public constructor(_map: L.Map) {
    this.p_map = _map;

    const markerIcon = GenerateCircleIcon(10, "black");
    this.p_marker = L.marker([0, 0], { icon: markerIcon, interactive: false }).addTo(_map);

    const accuracyCircle = L.circle([0, 0], { radius: 100, color: "black", fillColor: '*', fillOpacity: 0.3, interactive: false });
    this.p_accuracyCircle = accuracyCircle.addTo(_map);

    const fixedRadiusCircle = L.circleMarker([0, 0], { radius: 30, color: "black", fillColor: '*', fillOpacity: 0, interactive: false, dashArray: [8] });
    this.p_fixedCircle = fixedRadiusCircle.addTo(_map);

    const fixedRadiusCircleBg = L.circleMarker([0, 0], { radius: 30, color: "white", fillColor: '*', fillOpacity: 0, interactive: false, weight: 5 });
    this.p_fixedCircleBg = fixedRadiusCircleBg.addTo(_map).bringToBack();

    const svg = '<svg viewBox="0 0 24.00 24.00" fill="none" xmlns="http://www.w3.org/2000/svg" stroke="#0091ff" stroke-width="1.6799999999999997" transform="matrix(1, 0, 0, 1, 0, 0)rotate(-45)"><g id="SVGRepo_bgCarrier" stroke-width="0"/><g id="SVGRepo_tracerCarrier" stroke-linecap="round" stroke-linejoin="round" stroke="#CCCCCC" stroke-width="0.096"/><g id="SVGRepo_iconCarrier"> <path d="M18.9762 5.5914L14.6089 18.6932C14.4726 19.1023 13.8939 19.1023 13.7575 18.6932L11.7868 12.7808C11.6974 12.5129 11.4871 12.3026 11.2192 12.2132L5.30683 10.2425C4.89772 10.1061 4.89772 9.52743 5.30683 9.39106L18.4086 5.0238C18.7594 4.90687 19.0931 5.24061 18.9762 5.5914Z" stroke="#00aaff" stroke-linecap="round" stroke-linejoin="round"/> </g></svg>';
    const headingMarkerIcon = L.divIcon({
      className: "leaflet-data-marker",
      html: L.Util.template(svg, {}),
      iconAnchor: [25, 25],
      iconSize: [50, 50]
    });
    const headingMarkerOptions: L.MarkerOptions = {
      icon: headingMarkerIcon,
      draggable: false,
      keyboard: false,
      interactive: false,
    };
    this.p_headingMarker = L.marker([0, 0], headingMarkerOptions);

    const directionLine = L.polyline([[0, 0], [0, 0]], { color: 'black', fillColor: '*', fillOpacity: 0.3, interactive: false, dashArray: [8] });
    this.p_directionLine = directionLine.addTo(_map);

    this.p_reachCircleShort = L.circle([0, 0], {
      radius: 0,
      color: "#3388ff",
      weight: 2,
      dashArray: [6, 6],
      fillOpacity: 0.04,
      interactive: false,
      opacity: 0,
    }).addTo(_map);

    this.p_reachCircleLong = L.circle([0, 0], {
      radius: 0,
      color: "#3388ff",
      weight: 2,
      dashArray: [6, 6],
      fillOpacity: 0.02,
      interactive: false,
      opacity: 0,
    }).addTo(_map);

    this.p_reachLabelShort = L.marker([0, 0], {
      icon: CurrentLocationControl.createReachLabelIcon(""),
      interactive: false,
      opacity: 0,
    }).addTo(_map);

    this.p_reachLabelLong = L.marker([0, 0], {
      icon: CurrentLocationControl.createReachLabelIcon(""),
      interactive: false,
      opacity: 0,
    }).addTo(_map);

    _map.on('zoomstart', _e => {
      this.p_marker.remove();
      this.p_accuracyCircle.remove();
      this.p_fixedCircle.remove();
      this.p_fixedCircleBg.remove();
      this.p_headingMarker.remove();
      this.p_directionLine.remove();
      this.p_reachCircleShort.remove();
      this.p_reachCircleLong.remove();
      this.p_reachLabelShort.remove();
      this.p_reachLabelLong.remove();
    });
    _map.on('zoomend', _e => {
      this.p_marker.addTo(_map);
      this.p_accuracyCircle.addTo(_map);
      this.p_fixedCircle.addTo(_map);
      this.p_fixedCircleBg.addTo(_map).bringToBack();
      this.p_headingMarker.addTo(_map);
      this.p_directionLine.addTo(_map);
      this.p_reachCircleShort.addTo(_map);
      this.p_reachCircleLong.addTo(_map);
      this.p_reachLabelShort.addTo(_map);
      this.p_reachLabelLong.addTo(_map);
    });

    setInterval(() => this.cleanUpSpeedHistoryEntries(), 1000);
  }

  public updateLocationAndHeading(
    _lat: number,
    _lng: number,
    _accuracy: number,
    _heading: number | null,
    _speed: number | null
  ): void {
    this.p_marker
      .setLatLng([_lat, _lng]);

    this.p_accuracyCircle
      .setLatLng([_lat, _lng])
      .setRadius(_accuracy);

    this.p_fixedCircle
      .setLatLng([_lat, _lng]);

    this.p_fixedCircleBg
      .setLatLng([_lat, _lng]);

    if (_heading == null)
    {
      this.p_headingMarker.setOpacity(0);
    }
    else{
      this.p_headingMarker
        .setLatLng([_lat, _lng])
        .setRotationAngle(_heading)
        .setOpacity(1);
    }

    const now = Date.now();
    if (_speed != null)
      this.p_speedSamples.push({ time: now, speed: _speed });

    const avgSpeed = this.p_speedSamples.length > 0
      ? this.p_speedSamples.reduce((sum, s) => sum + s.speed, 0) / this.p_speedSamples.length
      : 0;

    if (avgSpeed > SPEED_THRESHOLD_MS) {
      const bounds = this.p_map.getBounds();
      const viewportHeightM = this.p_map.distance(
        [bounds.getNorth(), bounds.getCenter().lng],
        [bounds.getSouth(), bounds.getCenter().lng]
      );

      const shortTargetM = viewportHeightM * REACH_VIEWPORT_SHORT;
      const shortSnappedTimeS = CurrentLocationControl.snapTimeToStep(shortTargetM / avgSpeed);
      const longSnappedTimeS = CurrentLocationControl.nextTimeStep(shortSnappedTimeS);

      this.updateReachCircle(this.p_reachCircleShort, this.p_reachLabelShort, _lat, _lng, shortSnappedTimeS, avgSpeed, 0.04);
      this.updateReachCircle(this.p_reachCircleLong,  this.p_reachLabelLong,  _lat, _lng, longSnappedTimeS,  avgSpeed, 0.02);
    } else {
      this.p_reachCircleShort.setStyle({ opacity: 0, fillOpacity: 0 });
      this.p_reachCircleLong.setStyle({ opacity: 0, fillOpacity: 0 });
      this.p_reachLabelShort.setOpacity(0);
      this.p_reachLabelLong.setOpacity(0);
    }
  }

  public updateCompass(
    _heading: number | null
  ) {
    const center = this.p_marker.getLatLng();
    this.p_directionLine
      .setLatLngs([center, this.getDirectionLatLng(center.lat, center.lng, _heading, this.p_map.getBounds())]);
  }

  private cleanUpSpeedHistoryEntries(): void {
    const cutoff = Date.now() - 10_000;
    while (this.p_speedSamples.length > 0 && this.p_speedSamples[0].time < cutoff)
      this.p_speedSamples.shift();

    if (this.p_speedSamples.length === 0) {
      this.p_reachCircleShort.setStyle({ opacity: 0, fillOpacity: 0 });
      this.p_reachCircleLong.setStyle({ opacity: 0, fillOpacity: 0 });
      this.p_reachLabelShort.setOpacity(0);
      this.p_reachLabelLong.setOpacity(0);
    }
  }

  private updateReachCircle(
    _circle: L.Circle,
    _label: L.Marker,
    _lat: number,
    _lng: number,
    _snappedTimeS: number,
    _avgSpeedMs: number,
    _fillOpacity: number
  ): void {
    const radius = _snappedTimeS * _avgSpeedMs;
    const minutes = _snappedTimeS / 60;
    const labelText = minutes < 60 ? `${minutes} min` : `${(minutes / 60).toFixed(0)} h`;

    _circle
      .setLatLng([_lat, _lng])
      .setRadius(radius)
      .setStyle({ opacity: 1, fillOpacity: _fillOpacity });

    // 1 degree of latitude ≈ 111320 meters
    _label
      .setLatLng([_lat + radius / 111320, _lng])
      .setIcon(CurrentLocationControl.createReachLabelIcon(labelText))
      .setOpacity(1);
  }

  private static snapTimeToStep(_timeS: number): number {
    return REACH_TIME_STEPS_S.reduce((prev, curr) =>
      Math.abs(curr - _timeS) < Math.abs(prev - _timeS) ? curr : prev
    );
  }

  private static nextTimeStep(_currentTimeS: number): number {
    const idx = REACH_TIME_STEPS_S.indexOf(_currentTimeS);
    if (idx >= 0 && idx < REACH_TIME_STEPS_S.length - 1)
      return REACH_TIME_STEPS_S[idx + 1];
    
    return _currentTimeS;
  }

  private getDirectionLatLng(
    _lat: number,
    _lng: number,
    _directionDeg: number | null,
    _mapBounds: LatLngBounds | undefined
  ): LatLngExpression {
    if (_directionDeg === null)
      return [_lat, _lng];

    let lineLength = 5;
    if (_mapBounds !== undefined) {
      const viewHeight = Math.abs(_mapBounds.getNorth() - _mapBounds.getSouth());
      const viewWidth = Math.abs(_mapBounds.getEast() - _mapBounds.getWest());
      lineLength = Math.sqrt(viewHeight * viewHeight + viewWidth * viewWidth);
    }

    if (_directionDeg === 0)
      return [_lat + lineLength, _lng];
    if (_directionDeg === 90)
      return [_lat, _lng + lineLength];
    if (_directionDeg === 180)
      return [_lat - lineLength, _lng];
    if (_directionDeg === 270)
      return [_lat, _lng - lineLength];

    if (_directionDeg > 0 && _directionDeg < 180) {
      const latDiff = lineLength * Math.cos(_directionDeg * (Math.PI / 180));
      const lngDiff = Math.sqrt(lineLength * lineLength - latDiff * latDiff);
      return [_lat + latDiff, _lng + lngDiff];
    }
    if (_directionDeg > 180 && _directionDeg < 360) {
      const latDiff = lineLength * Math.cos(_directionDeg * (Math.PI / 180));
      const lngDiff = Math.sqrt(lineLength * lineLength - latDiff * latDiff);
      return [_lat + latDiff, _lng - lngDiff];
    }

    return [_lat, _lng];
  }

  private static createReachLabelIcon(_text: string): L.DivIcon {
    const icon = this.p_reachCircleMarkerCache.get(_text);
    if (icon)
      return icon;

    const newIcon = L.divIcon({
      className: "",
      iconSize: [0, 0],
      iconAnchor: [0, 0],
      html: `<div style="
        background: rgba(255,255,255,0);
        border: 1px solid #3388ff00;
        border-radius: 4px;
        padding: 1px 5px;
        font-size: 13px;
        font-weight: 600;
        color: #1a56cc;
        white-space: nowrap;
        pointer-events: none;
        text-shadow: 0 0 3px #fff, 0 0 3px #fff, 0 0 3px #fff;
      ">${_text}</div>`,
    });
    this.p_reachCircleMarkerCache.set(_text, newIcon);
    return newIcon;
  }

}