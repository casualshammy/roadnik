import { GenerateCircleIcon } from "../toolkit/mapToolkit";
import L, { LatLngBounds, type LatLngExpression } from "leaflet";

export class CurrentLocationControl {
  private readonly p_map: L.Map;
  private readonly p_marker: L.Marker;
  private readonly p_accuracyCircle: L.Circle;
  private readonly p_fixedCircle: L.CircleMarker;
  private readonly p_fixedCircleBg: L.CircleMarker;
  private readonly p_directionLine: L.Polyline;
  private readonly p_movingLine: L.Polyline;

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

    const movingLine = L.polyline([[0, 0], [0, 0]], { color: 'red', fillColor: '*', fillOpacity: 0.3, interactive: false, dashArray: [8] });
    this.p_movingLine = movingLine.addTo(_map);

    const directionLine = L.polyline([[0, 0], [0, 0]], { color: 'black', fillColor: '*', fillOpacity: 0.3, interactive: false, dashArray: [8] });
    this.p_directionLine = directionLine.addTo(_map);
  }

  public updateLocationAndHeading(
    _lat: number,
    _lng: number,
    _accuracy: number,
    _heading: number | null
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

    this.p_movingLine
      .setLatLngs([[_lat, _lng], this.getDirectionLatLng(_lat, _lng, _heading, this.p_map.getBounds())]);
  }

  public updateCompass(
    _heading: number | null
  ) {
    const center = this.p_marker.getLatLng();
    this.p_directionLine
      .setLatLngs([center, this.getDirectionLatLng(center.lat, center.lng, _heading, this.p_map.getBounds())]);
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

}