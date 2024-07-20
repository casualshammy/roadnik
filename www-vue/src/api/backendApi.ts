import type { GetPathResData, WsBaseMsg } from '@/data/backend';
import { LatLng } from 'leaflet';
import { WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

type ListRoomPointsResData = {
  PointId: number;
  Username: string;
  Lat: number;
  Lng: number;
  Description: string;
}

type DeleteRoomPointReq = {
  RoomId: string;
  PointId: number;
}

type CreateNewPointReq = {
  RoomId: string;
  Username: string;
  Lat: number;
  Lng: number;
  Description: string;
}

export class BackendApi {
  private readonly p_apiUrl: string;

  constructor() {
    if (import.meta.env.MODE === "development")
      this.p_apiUrl = "http://localhost:5544";
    else
    this.p_apiUrl = "..";
  }

  public setupWs(_roomId: string, _listener: (ws: Websocket, msg: WsBaseMsg) => Promise<void>): Websocket {
    const host = window.document.location.host.replace(/\/+$/, "");
    const path = window.document.location.pathname.replace(/\/+$/, "");
    const protocol = window.document.location.protocol === "https:" ? "wss:" : "ws:";
    const url = import.meta.env.MODE === "development" ? `ws://localhost:5544/ws?roomId=${_roomId}` : `${protocol}//${host}${path}/../ws?roomId=${_roomId}`
    const ws = new WebsocketBuilder(url)
      .onMessage((_ws, _ev) => _listener(_ws, JSON.parse(_ev.data)))
      .withBackoff(new ConstantBackoff(1000))
      .build();
    return ws;
  }

  public async getPathsAsync(_roomId: string, _offset: number | undefined = 0): Promise<GetPathResData> {
    const response = await fetch(`${this.p_apiUrl}/get?roomId=${_roomId}&offset=${_offset}`);
    const data: GetPathResData = await response.json();
    return data;
  }

  public async listPointsAsync(_roomId: string): Promise<ListRoomPointsResData[]> {
    const response = await fetch(`${this.p_apiUrl}/list-room-points?roomId=${_roomId}`);
    const data: ListRoomPointsResData[] = await response.json();
    return data;
  }

  public async createPointAsync(_roomId: string, _username: string, _latLng: LatLng, _description: string): Promise<boolean> {
    const data: CreateNewPointReq = {
      RoomId: _roomId,
      Username: _username,
      Lat: _latLng.lat,
      Lng: _latLng.lng,
      Description: _description
    };

    const res = await fetch(`${this.p_apiUrl}/create-new-point`, {
      method: "POST",
      body: JSON.stringify(data),
      headers: {
        "Content-type": "application/json; charset=UTF-8"
      }
    });
    return res.ok;
  }

  public async deletePointAsync(_roomId: string, _pointId: number): Promise<void> {
    const data: DeleteRoomPointReq = {
      RoomId: _roomId,
      PointId: _pointId
    };

    const res = await fetch(`${this.p_apiUrl}/delete-room-point`, {
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
    const res = await fetch(`${this.p_apiUrl}/is-room-id-valid?roomId=${_roomId}`, {
      method: "GET"
    });

    return !(res.status === 406);
  }

}
