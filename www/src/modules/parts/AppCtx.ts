import Cookies from "js-cookie";
import { IMapState } from "../api";
import { DEFAULT_MAP_LAYER, GetMapStateFromCookie } from "../maps";
import { COOKIE_MAP_LAYER, COOKIE_MAP_OVERLAY, COOKIE_MAP_STATE, COOKIE_SELECTED_PATH, COOKIE_SELECTED_PATH_BOTTOM, COOKIE_SELECTED_PATH_LEFT } from "../consts";
import { base64ToUtf8Text } from "../toolkit";

export interface IAppCtx {
    readonly isRoadnikApp: boolean;
    readonly roomId: string | null;
    lastTracksOffset: number;
    currentLocationMarker: L.Marker | undefined;
    currentLocationCircle: L.Circle | undefined;
    userColorIndex: number;
    readonly userColors: Map<string, string>;
    firstTracksSyncCompleted: boolean;
    maxTrackPoints: number;
    readonly mapState: IMapState;
}

export function CreateAppCtx(_layers: L.Control.LayersObject, _overlays: L.Control.LayersObject): IAppCtx {
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);

    const isRoadnikApp = navigator.userAgent.includes("RoadnikApp");

    const cookieLatLngZoom = !isRoadnikApp ? GetMapStateFromCookie(Cookies.get(COOKIE_MAP_STATE)) : null;
    let lat = parseFloat(urlParams.get('lat') ?? "");
    if (Number.isNaN(lat)) {
        if (cookieLatLngZoom !== null)
            lat = cookieLatLngZoom.Lat;
        else
            lat = 51.4768;
    }

    let lng = parseFloat(urlParams.get('lng') ?? "");
    if (Number.isNaN(lng)) {
        if (cookieLatLngZoom !== null)
            lng = cookieLatLngZoom.Lng;
        else
            lng = 0.0006;
    }

    let zoom = parseInt(urlParams.get('zoom') ?? "");
    if (Number.isNaN(zoom)) {
        if (cookieLatLngZoom !== null)
            zoom = cookieLatLngZoom.Zoom;
        else
            zoom = 14;
    }
    console.log(`Initial view: ${lat}/${lng}/${zoom}`);

    let layer = DEFAULT_MAP_LAYER;
    const urlParamLayout = urlParams.get('layer');
    const cookieLayout = !isRoadnikApp ? Cookies.get(COOKIE_MAP_LAYER) : null;
    if (urlParamLayout !== null && _layers[urlParamLayout] !== undefined)
        layer = urlParamLayout;
    else if (cookieLayout !== null && cookieLayout !== undefined && _layers[cookieLayout] !== undefined)
        layer = cookieLayout;
    console.log(`Initial layer: ${layer}`);

    const overlays: string[] = [];
    const urlParamOverlays = urlParams.get('overlays');
    const cookieOverlay = !isRoadnikApp ? Cookies.get(COOKIE_MAP_OVERLAY) : null;
    if (urlParamOverlays !== null) {
        const overlaysJson = base64ToUtf8Text(urlParamOverlays);
        const rawOverlays = JSON.parse(overlaysJson) as string[];
        for (const overlay of rawOverlays) {
            const overlayLayer = Object.entries(_overlays).find((_v, _i, _o) => _v[0] === overlay);
            if (overlayLayer !== undefined)
                overlays.push(overlay);
        }
    }
    else if (cookieOverlay !== undefined && cookieOverlay !== null) {
        const rawOverlays = JSON.parse(cookieOverlay) as string[];
        for (const overlay of rawOverlays) {
            const overlayLayer = Object.entries(_overlays).find((_v, _i, _o) => _v[0] === overlay);
            if (overlayLayer !== undefined)
                overlays.push(overlay);
        }
    }
    console.log(`Initial overlays: ${overlays.length}`);

    let selectedPath: string | null = null;
    const urlParamSelectedUser = urlParams.get('selected_path');
    const cookieSelectedUser = !isRoadnikApp ? Cookies.get(COOKIE_SELECTED_PATH) : null;
    if (urlParamSelectedUser !== null)
        selectedPath = urlParamSelectedUser;
    else if (cookieSelectedUser !== null && cookieSelectedUser !== undefined)
        selectedPath = cookieSelectedUser;
    console.log(`Initial selected path: ${selectedPath}`);

    let selectedPathWindowLeft: number | null = null;
    const urlParamSelectedPathWindowLeft = urlParams.get('selected_path_window_left');
    const cookieSelectedPathWindowLeft = !isRoadnikApp ? Cookies.get(COOKIE_SELECTED_PATH_LEFT) : null;
    if (urlParamSelectedPathWindowLeft !== null) {
        const fl = parseFloat(urlParamSelectedPathWindowLeft);
        if (fl > 0 && fl < (window.innerWidth - 10))
            selectedPathWindowLeft = fl;
    }
    else if (cookieSelectedPathWindowLeft !== null && cookieSelectedPathWindowLeft !== undefined) {
        const fl = parseFloat(cookieSelectedPathWindowLeft);
        if (fl > 0 && fl < (window.innerWidth - 10))
            selectedPathWindowLeft = fl;
    }

    let selectedPathWindowBottom: number | null = null;
    const urlParamSelectedPathWindowBottom = urlParams.get('selected_path_window_bottom');
    const cookieSelectedPathWindowBottom = !isRoadnikApp ? Cookies.get(COOKIE_SELECTED_PATH_BOTTOM) : null;
    if (urlParamSelectedPathWindowBottom !== null) {
        const fl = parseFloat(urlParamSelectedPathWindowBottom);
        if (fl > 0 && fl < (window.innerHeight - 10))
            selectedPathWindowBottom = fl;
    }
    else if (cookieSelectedPathWindowBottom !== null && cookieSelectedPathWindowBottom !== undefined) {
        const fl = parseFloat(cookieSelectedPathWindowBottom);
        if (fl > 0 && fl < (window.innerHeight - 10))
            selectedPathWindowBottom = fl;
    }

    return {
        isRoadnikApp: isRoadnikApp,
        roomId: urlParams.get('id'),
        lastTracksOffset: 0,
        currentLocationMarker: undefined,
        currentLocationCircle: undefined,
        userColorIndex: 0,
        userColors: new Map<string, string>(),
        firstTracksSyncCompleted: false,
        maxTrackPoints: 1000,
        mapState: {
            lat: lat,
            lng: lng,
            zoom: zoom,
            layer: layer,
            overlays: overlays,
            selectedPath: selectedPath,
            selectedPathWindowLeft: selectedPathWindowLeft,
            selectedPathWindowBottom: selectedPathWindowBottom
        }
    };
}