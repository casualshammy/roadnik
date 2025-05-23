import { CurrentLocationControl } from "@/components/CurrentLocationControl";
import type { AppCtx } from "@/data/AppCtx";
import type { TimedStorageEntry } from "@/data/backend";
import Cookies from "js-cookie";
import type { Reactive, ShallowRef } from "vue";
import * as Consts from '../data/Consts';
import type { HostApi } from "@/api/hostApi";

export class MapInteractor {
  private readonly p_appCtx: AppCtx;
  private readonly p_hostApi: HostApi;
  private readonly p_map: ShallowRef<L.Map | undefined>;
  private readonly p_paths: Reactive<Map<string, L.Polyline>>;
  private readonly p_geoEntries: Reactive<Map<string, TimedStorageEntry[]>>;

  constructor(
    _appCtx: AppCtx,
    _hostApi: HostApi,
    _map: ShallowRef<L.Map | undefined>,
    _paths: Reactive<Map<string, L.Polyline>>,
    _geoEntries: Reactive<Map<string, TimedStorageEntry[]>>
  ) {
    this.p_appCtx = _appCtx;
    this.p_hostApi = _hostApi;
    this.p_map = _map;
    this.p_paths = _paths;
    this.p_geoEntries = _geoEntries;

    (window as any).setMapCenter = this.setMapCenter.bind(this);
    (window as any).setLocationAndHeading = this.setLocationAndHeading.bind(this);
    (window as any).setCompassHeading = this.setCompassHeading.bind(this);
    (window as any).setMapCenterToUser = this.setMapCenterToUser.bind(this); // setViewToTrack
    (window as any).setMapCenterToAllUsers = this.setMapCenterToAllUsers.bind(this); // setViewToAllTracks
    (window as any).setObservedUser = this.setObservedUser.bind(this);
  }

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

  public setLocationAndHeading(
    _lat: number,
    _lng: number,
    _accuracy: number,
    _heading: number | null
  ): boolean {
    const map = this.p_map.value;
    if (map === undefined)
      return false;

    if (this.p_appCtx.currentLocation === null) {
      this.p_appCtx.currentLocation = new CurrentLocationControl(map);
      console.log("Created current location marker");
    }

    this.p_appCtx.currentLocation.updateLocationAndHeading(_lat, _lng, _accuracy, _heading);
    return true;
  }

  public setCompassHeading(
    _heading: number | null
  ): boolean {
    const map = this.p_map.value;
    if (map === undefined)
      return false;

    if (this.p_appCtx.currentLocation === null) {
      this.p_appCtx.currentLocation = new CurrentLocationControl(map);
      console.log("Created current location marker");
    }

    this.p_appCtx.currentLocation.updateCompass(_heading);
    return true;
  }

  public setMapCenterToUser(
    _user: string,
    _zoom?: number | undefined
  ): boolean {
    const map = this.p_map.value;
    if (map === undefined)
      return false;

    const points = this.p_geoEntries.get(_user);
    if (points === undefined || points.length === 0)
      return false;

    const lastLocation = points[points.length - 1];
    this.setMapCenter(lastLocation.Latitude, lastLocation.Longitude, _zoom, 500);
    return true;
  }

  public setMapCenterToAllUsers(): boolean {
    const map = this.p_map.value;
    if (map === undefined)
      return false;

    const paths = this.p_paths;
    if (paths.size > 0) {
      let bounds: L.LatLngBoundsExpression | undefined = undefined;
      for (let path of paths.values())
        if (bounds === undefined)
          bounds = path.getBounds();
        else
          bounds = bounds.extend(path.getBounds());

      if (bounds !== undefined)
        map.fitBounds(bounds);
    }

    return true;
  }

  public setObservedUser(
    _user: string | null,
    _log: boolean = true
  ): boolean {
    this.p_appCtx.mapState.value.selectedPath = _user;

    if (_user === null) {
      if (!this.p_appCtx.isRoadnikApp)
        Cookies.remove(Consts.COOKIE_SELECTED_PATH);
      else
        this.p_hostApi.sendMapStateToRoadnikApp();

      if (_log)
        console.log(`Selected path is cleared`);

      return true;
    }

    if (!this.p_appCtx.isRoadnikApp)
      Cookies.set(Consts.COOKIE_SELECTED_PATH, _user);
    else
      this.p_hostApi.sendMapStateToRoadnikApp();

    if (_log)
      console.log(`Selected path is set to ${_user}`);

    return true;
  }

}