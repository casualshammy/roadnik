import { GenerateCircleIcon } from "../toolkit/mapToolkit";
import L from "leaflet";

export class CurrentLocationControl {
  private readonly p_marker: L.Marker;
  private readonly p_accuracyCircle: L.Circle;
  private readonly p_fixedCircle: L.CircleMarker;
  private readonly p_fixedCircleBg: L.CircleMarker;

  public constructor(_map: L.Map) {
    const markerIcon = GenerateCircleIcon(10, "black");
    this.p_marker = L.marker([0, 0], { icon: markerIcon, interactive: false }).addTo(_map);;

    const accuracyCircle = L.circle([0, 0], { radius: 100, color: "black", fillColor: '*', fillOpacity: 0.3, interactive: false });
    this.p_accuracyCircle = accuracyCircle.addTo(_map);

    const fixedRadiusCircle = L.circleMarker([0, 0], { radius: 30, color: "black", fillColor: '*', fillOpacity: 0, interactive: false, dashArray: [8] });
    this.p_fixedCircle = fixedRadiusCircle.addTo(_map);

    const fixedRadiusCircleBg = L.circleMarker([0, 0], { radius: 30, color: "white", fillColor: '*', fillOpacity: 0, interactive: false, weight: 5 });
    this.p_fixedCircleBg = fixedRadiusCircleBg.addTo(_map).bringToBack();
  }

  public updateLocation(_lat: number, _lng: number, _accuracy: number) {
    this.p_marker
      .setLatLng([_lat, _lng]);

    this.p_accuracyCircle
      .setLatLng([_lat, _lng])
      .setRadius(_accuracy);

    this.p_fixedCircle
      .setLatLng([_lat, _lng]);

    this.p_fixedCircleBg
      .setLatLng([_lat, _lng]);
  }
}