import type { AppCtx } from "@/data/AppCtx";
import * as Consts from '../data/Consts';
import type { LatLngZoom } from "@/data/LatLngZoom";
import type { Ref } from "vue";

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

  constructor(
    _appCtx: AppCtx,
    _mapLocation: Ref<LatLngZoom>
  ) {
    this.p_appCtx = _appCtx;
    this.p_mapLocation = _mapLocation;

    (window as any).setLocation = this.setLocation;
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

  // C# => JS

  public setLocation(_x: number, _y: number, _zoom?: number | undefined): boolean {
    this.p_mapLocation.value = { lat: _x, lng: _y, zoom: _zoom };
    return true;
  }

  private sendDataToHost(_msg: JsToCSharpMsg): void {
    try {
      const json = JSON.stringify(_msg);
      (window as any).jsBridge.invokeAction(json);
    }
    catch { }
  }

}