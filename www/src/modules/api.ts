import { LatLng } from 'leaflet';
import { WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

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
}

export interface GetPathResData {
  LastUpdateUnixMs: number;
  MoreEntriesAvailable: boolean;
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

export interface JsToCSharpMsg {
  msgType: string;
  data: any;
}

export interface HostMsgTracksSynchronizedData {
  isFirstSync: boolean;
}

export interface IMapState {
  lat: number;
  lng: number;
  zoom: number;
  layer: string;
  overlays: string[];
  selectedPath: string | null;
  selectedPathWindowLeft: number | null;
  selectedPathWindowBottom: number | null;
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

  public async isRoomIdValidAsync(_roomId?: string | undefined | null): Promise<boolean> {
    const res = await fetch(`../is-room-id-valid?roomId=${_roomId}`, {
      method: "GET"
    });

    return !(res.status === 406);
  }

}
