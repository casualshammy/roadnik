import type { AppCtx } from "@/data/AppCtx";
import * as Consts from '../data/Consts';

export type MapState = {
  lat: number;
  lng: number;
  zoom: number;
  layer: string;
  overlays: string[];
  selectedAppId: string | null;
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

  constructor(
    _appCtx: AppCtx
  ) {
    this.p_appCtx = _appCtx;
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