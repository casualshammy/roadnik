import { GenerateCircleIcon } from "../toolkit/mapToolkit";
import L, { LatLngBounds, type LatLngExpression } from "leaflet";

export class CurrentLocationControl {
  private readonly p_marker: L.Marker;
  private readonly p_accuracyCircle: L.Circle;
  private readonly p_fixedCircle: L.CircleMarker;
  private readonly p_fixedCircleBg: L.CircleMarker;
  private readonly p_directionLine: L.Polyline;

  public constructor(_map: L.Map) {
    const markerIcon = GenerateCircleIcon(10, "black");
    this.p_marker = L.marker([0, 0], { icon: markerIcon, interactive: false }).addTo(_map);

    const accuracyCircle = L.circle([0, 0], { radius: 100, color: "black", fillColor: '*', fillOpacity: 0.3, interactive: false });
    this.p_accuracyCircle = accuracyCircle.addTo(_map);

    const fixedRadiusCircle = L.circleMarker([0, 0], { radius: 30, color: "black", fillColor: '*', fillOpacity: 0, interactive: false, dashArray: [8] });
    this.p_fixedCircle = fixedRadiusCircle.addTo(_map);

    const fixedRadiusCircleBg = L.circleMarker([0, 0], { radius: 30, color: "white", fillColor: '*', fillOpacity: 0, interactive: false, weight: 5 });
    this.p_fixedCircleBg = fixedRadiusCircleBg.addTo(_map).bringToBack();

    const directionLine = L.polyline([[0,0], [0,0]], {color: 'black', fillColor : '*', fillOpacity: 0.3, interactive: false, dashArray: [8]});
    this.p_directionLine = directionLine.addTo(_map);
  }

  public updateLocation(
    _lat: number, 
    _lng: number, 
    _accuracy: number,
    _arc: number | null,
    _mapBounds: LatLngBounds | undefined
  ) {
    this.p_marker
      .setLatLng([_lat, _lng]);

    this.p_accuracyCircle
      .setLatLng([_lat, _lng])
      .setRadius(_accuracy);

    this.p_fixedCircle
      .setLatLng([_lat, _lng]);

    this.p_fixedCircleBg
      .setLatLng([_lat, _lng]);

    this.p_directionLine
      .setLatLngs([[_lat, _lng], this.getDirectionLatLng(_lat, _lng, _arc, _mapBounds)]);
  }

  private getDirectionLatLng(
    _lat: number, 
    _lng: number, 
    _arc: number | null,
    _mapBounds: LatLngBounds | undefined
  ) : LatLngExpression {
    let lineLength = 5;
    if (_mapBounds !== undefined) {
      const viewHeight = Math.abs(_mapBounds.getNorth() - _mapBounds.getSouth());
      const viewWidth = Math.abs(_mapBounds.getEast() - _mapBounds.getWest());
      const viewMinSize = Math.min(viewHeight, viewWidth);
      lineLength = viewMinSize/3;
    }
    
    if (_arc === null)
      return [_lat, _lng];
    if (_arc === 0) 
      return [_lat + lineLength, _lng];
    if (_arc === 90)
      return [_lat, _lng + lineLength];
    if (_arc === 180)
      return [_lat - lineLength, _lng];
    if (_arc === 270)
      return [_lat, _lng - lineLength];

    if (_arc > 0 && _arc < 180) {
      const latDiff = lineLength * Math.cos(_arc * (Math.PI/180));
      const lngDiff = Math.sqrt(Math.pow(lineLength, 2) - Math.pow(latDiff, 2));
      return [_lat + latDiff, _lng + lngDiff];
    }
    if (_arc > 180 && _arc < 360) {
      const latDiff = lineLength * Math.cos(_arc * (Math.PI/180));
      const lngDiff = Math.sqrt(Math.pow(lineLength, 2) - Math.pow(latDiff, 2));
      return [_lat + latDiff, _lng - lngDiff];
    }

    return [_lat, _lng];
  }

}