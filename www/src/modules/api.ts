import { LatLng } from 'leaflet';
import { WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

export const WS_MSG_TYPE_HELLO: string = "ws-msg-hello";
export const WS_MSG_TYPE_DATA_UPDATED: string = "ws-msg-data-updated";
export const WS_MSG_PATH_WIPED: string = "ws-msg-path-wiped";
export const WS_MSG_ROOM_POINTS_UPDATED: string = "ws-msg-room-points-updated";

export const JS_TO_CSHARP_MSG_TYPE_APP_LOADED = "js-msg-app-loaded";
export const HOST_MSG_REQUEST_DONE = "host-msg-request-done";
export const JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED = "js-msg-map-location-changed";
export const JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED = "js-msg-map-layer-changed";
export const HOST_MSG_NEW_POINT = "host-msg-new-point";
export const JS_TO_CSHARP_MSG_TYPE_POPUP_OPENED = "js-msg-popup-opened";
export const JS_TO_CSHARP_MSG_TYPE_POPUP_CLOSED = "js-msg-popup-closed";
export const JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED = "js-msg-waypoint-add-started";

export interface TimedStorageEntry {
    UnixTimeMs: number;
    RoomId: string;
    Username: string;
    Latitude: number;
    Longitude: number;
    Altitude: number;
    Speed?: number | undefined | null;
    Accuracy?: number | undefined | null;
    Battery?: number | undefined | null;
    GsmSignal?: number | undefined | null;
    Bearing?: number | undefined | null;
    Message?: string | undefined | null;
}

export interface GetPathResData {
    LastUpdateUnixMs: number;
    Entries: TimedStorageEntry[];
}

export interface ListRoomPointsResData {
    PointId: number;
    Username: string;
    Lat: number;
    Lng: number;
    Description: string;
}

export interface WsBaseMsg {
    Type: string;
    Payload: any;
}

export interface WsMsgHello {
    UnixTimeMs: number;
    MaxPathPointsPerRoom: number;
}

export interface WsMsgPathWiped {
    Username: string;
}

export interface MapViewState {
    location: L.LatLng;
    zoom: number;
}

export interface JsToCSharpMsg {
    msgType: string;
    data: any;
}

export interface HostMsgRequestDoneData {
    dataReceived: boolean;
    firstDataPart: boolean;
}

interface DeleteRoomPointReq {
    RoomId: string;
    PointId: number;
}

interface CreateNewPointReq {
    RoomId: string;
    Username: string;
    Lat: number;
    Lng: number;
    Description: string;
}

export class StorageApi {
    constructor() {

    }

    public async getDataAsync(_roomId: string, _offset: number | undefined = 0): Promise<GetPathResData> {
        const response = await fetch(`../get?roomId=${_roomId}&offset=${_offset}`);
        const data: GetPathResData = await response.json();
        return data;
    }

    public setupWs(_roomId: string, _listener: (ws: Websocket, msg: WsBaseMsg) => Promise<void>): Websocket {
        const host = window.document.location.host.replace(/\/+$/, "");
        const path = window.document.location.pathname.replace(/\/+$/, "");
        const protocol = window.document.location.protocol === "https:" ? "wss:" : "ws:";
        const ws = new WebsocketBuilder(`${protocol}//${host}${path}/../ws?roomId=${_roomId}`)
            .onMessage((_ws, _ev) => _listener(_ws, JSON.parse(_ev.data)))
            .withBackoff(new ConstantBackoff(1000))
            .build();
        return ws;
    }

    public async listRoomPointsAsync(_roomId: string): Promise<ListRoomPointsResData[]> {
        const response = await fetch(`../list-room-points?roomId=${_roomId}`);
        const data: ListRoomPointsResData[] = await response.json();
        return data;
    }

    public async createRoomPointAsync(_roomId: string, _username: string, _latLng: LatLng, _description: string): Promise<boolean> {
        const data: CreateNewPointReq = {
            RoomId: _roomId,
            Username: _username,
            Lat: _latLng.lat,
            Lng: _latLng.lng,
            Description: _description
        };

        const res = await fetch(`../create-new-point`, {
            method: "POST",
            body: JSON.stringify(data),
            headers: {
                "Content-type": "application/json; charset=UTF-8"
            }
        });
        return res.ok;
    }

    public async deleteRoomPointAsync(_roomId: string, _pointId: number): Promise<void> {
        const data: DeleteRoomPointReq = {
            RoomId: _roomId,
            PointId: _pointId
        };

        const res = await fetch(`../delete-room-point`, {
            method: "POST",
            body: JSON.stringify(data),
            headers: {
                "Content-type": "application/json; charset=UTF-8"
            }
        });

        if (res.status === 429)
            alert("You're deleting points too fast; please wait a second");
    }

}
