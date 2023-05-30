import { WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

export const WS_MSG_TYPE_HELLO: string = "ws-msg-hello";
export const WS_MSG_TYPE_DATA_UPDATED: string = "ws-msg-data-updated";
export const WS_MSG_PATH_WIPED: string = "ws-msg-path-wiped";
export const WS_MSG_ROOM_POINTS_UPDATED: string = "ws-msg-room-points-updated";

export const JS_TO_CSHARP_MSG_TYPE_APP_LOADED = "js-msg-app-loaded";
export const JS_TO_CSHARP_MSG_TYPE_INITIAL_DATA_RECEIVED = "js-msg-initial-data-received";
export const JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED = "js-msg-map-location-changed";
export const JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED = "js-msg-map-layer-changed";
export const JS_TO_CSHARP_MSG_TYPE_NEW_TRACK = "js-msg-new-track";
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

export interface GetResData {
    Success: boolean;
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

interface DeleteRoomPointReq {
    RoomId: string;
    PointId: number;
}

export class StorageApi {
    constructor() {

    }

    public async getDataAsync(_roomId: string, _offset: number | undefined = 0): Promise<GetResData> {
        const response = await fetch(`../get?roomId=${_roomId}&offset=${_offset}`);
        const data: GetResData = await response.json();
        return data;
    }

    public setupWs(_roomId: string, _listener: (ws: Websocket, msg: WsBaseMsg) => any): Websocket {
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

    public async deleteRoomPointAsync(_roomId: string, _pointId: number): Promise<boolean> {
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
        return res.ok;
    }

}
