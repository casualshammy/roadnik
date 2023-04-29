import {WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

export const WS_MSG_TYPE_HELLO: string = "ws-msg-hello";
export const WS_MSG_TYPE_DATA_UPDATED: string = "ws-msg-data-updated";

export interface StorageEntry {
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
    LastUpdateUnixMs?: number | undefined | null;
    LastEntry?: StorageEntry | undefined | null; 
    Entries: StorageEntry[];
}

export interface WsBaseMsg {
    Type: string;
    Payload: any;
}

export interface WebAppState {
    location: L.LatLng;
    zoom: number;
    mapLayer?: string | undefined;
    autoPan: boolean;
}

export class StorageApi {
    constructor() {
        
    }

    public async getDataAsync(_key: string, _entriesLimit: number | undefined = 100, _offset: number | undefined = 0): Promise<GetResData> {
        const response = await fetch(`get?key=${_key}&limit=${_entriesLimit}&offset=${_offset}`);
        const data: GetResData = await response.json();
        return data;
    }

    public setupWs(_key: string, _listener: (ws: Websocket, msg: WsBaseMsg) => any): Websocket {
        const host = window.document.location.host.replace(/\/+$/, "");
        const path = window.document.location.pathname.replace(/\/+$/, "");
        const protocol = window.document.location.protocol === "https:" ? "wss:" : "ws:";
        const ws = new WebsocketBuilder(`${protocol}//${host}${path}/ws?key=${_key}`)
            .onMessage((_ws, _ev) => _listener(_ws, JSON.parse(_ev.data)))
            .withBackoff(new ConstantBackoff(10000))
            .build();
        return ws;
    }
}
