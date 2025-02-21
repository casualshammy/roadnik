import type { AppCtx } from "@/data/AppCtx";
import * as Consts from '../data/Consts';
import type { LatLngZoom } from "@/data/LatLngZoom";
import type { Ref, ShallowRef } from "vue";
import { sampleTime, Subject } from "rxjs";
import { CurrentLocationControl } from "@/components/CurrentLocationControl";

export type MapState = {
  lat: number;
  lng: number;
  zoom: number;
  layer: string;
  overlays: string[];
  selectedPath: string | null;
  selectedPathWindowLeft: number | null;
  selectedPathWindowBottom: number | null;
}

type JsToCSharpMsg = {
  msgType: string;
  data: any;
}

type HostMsgTracksSynchronizedData = {
  isFirstSync: boolean;
}

export class HostApi {
  private readonly p_appCtx: AppCtx;
  private readonly p_mapLocation: Ref<LatLngZoom>;
  private readonly p_map: ShallowRef<L.Map | undefined>;

  constructor(
    _appCtx: AppCtx,
    _mapLocation: Ref<LatLngZoom>,
    _map: ShallowRef<L.Map | undefined>
  ) {
    this.p_appCtx = _appCtx;
    this.p_mapLocation = _mapLocation;
    this.p_map = _map;

    (window as any).setMapCenter = this.setMapCenter.bind(this);
    (window as any).setCurrentLocation = this.setCurrentLocation.bind(this);
  }

  // JS => C#

  public sendMapStateToRoadnikApp(): void {
    const state = this.p_appCtx.mapState;
    this.sendDataToHost({ msgType: Consts.HOST_MSG_MAP_STATE, data: state.value });
  }

  public sendWaypointAddStarted(_latLng: L.LatLng) {
    this.sendDataToHost({ msgType: Consts.JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED, data: _latLng });
  }

  public sendTracksSynchronized(_isFirstSync: boolean) {
    const data: HostMsgTracksSynchronizedData = {
      isFirstSync: _isFirstSync
    }
    this.sendDataToHost({ msgType: Consts.HOST_MSG_TRACKS_SYNCHRONIZED, data: data });
  }

  public sendMapDragStarted() {
    this.sendDataToHost({ msgType: Consts.HOST_MSG_MAP_DRAG_STARTED, data: {} });
  }

  // C# => JS

  public setMapCenter(
    _lat: number, 
    _lng: number, 
    _zoom?: number, 
    _animationMs?: number
  ): boolean {
    var map = this.p_map.value;
    if (map === undefined)
      return false;

    if (_animationMs !== undefined)
      map.setView([_lat, _lng], _zoom, { animate: true, duration: _animationMs / 1000 });
    else
      map.setView([_lat, _lng], _zoom, { animate: false });

    return true;
  }

  public setCurrentLocation(
    _lat: number, 
    _lng: number, 
    _accuracy: number, 
    _directionDeg: number | null
  ): boolean {
    const map = this.p_map.value;
    if (map === undefined)
      return false;

    if (this.p_appCtx.currentLocation === null) {
      this.p_appCtx.currentLocation = new CurrentLocationControl(map);
      console.log("Created current location marker");
    }
  
    this.p_appCtx.currentLocation.updateLocation(_lat, _lng, _accuracy, _directionDeg, map.getBounds());
  
    //console.log(`New current location: ${_lat},${_lng}; accuracy: ${_accuracy}; heading: ${_directionDeg?.toFixed(0)}`);
    return true;
  }

  private sendDataToHost(_msg: JsToCSharpMsg): void {
    try {
      const json = JSON.stringify(_msg);
      (window as any).jsBridge.invokeAction(json);
    }
    catch (_error) {
      console.error(`Can't send data to Roadnik app: ${_error}`);
    }
  }

}