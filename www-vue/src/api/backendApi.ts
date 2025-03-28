import type { GetPathResData, WsBaseMsg } from '@/data/backend';
import { LatLng } from 'leaflet';
import { WebsocketBuilder, ConstantBackoff, Websocket } from 'websocket-ts';

type RoomPoint = {
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

  constructor(_apiUrl: string) {
    this.p_apiUrl = _apiUrl;
  }

  public setupWs(_roomId: string, _listener: (ws: Websocket, msg: WsBaseMsg) => Promise<void>): Websocket {
    const wsApiUrl = this.p_apiUrl.replace(/^http/, "ws");
    const url = `${wsApiUrl}/api/v1/ws?roomId=${_roomId}`
    const ws = new WebsocketBuilder(url)
      .onMessage((_ws, _ev) => _listener(_ws, JSON.parse(_ev.data)))
      .withBackoff(new ConstantBackoff(1000))
      .build();
    return ws;
  }

  public async getPathsAsync(_roomId: string, _offset: number | undefined = 0): Promise<GetPathResData> {
    const response = await fetch(`${this.p_apiUrl}/api/v1/list-room-path-points?roomId=${_roomId}&offset=${_offset}`);
    const data: GetPathResData = await response.json();
    return data;
  }

  public async listPointsAsync(_roomId: string): Promise<RoomPoint[]> {
    const response = await fetch(`${this.p_apiUrl}/api/v1/list-room-points?roomId=${_roomId}`);
    const data: { Result: RoomPoint[] } = await response.json();
    return data.Result;
  }

  public async createPointAsync(_roomId: string, _username: string, _latLng: LatLng, _description: string): Promise<boolean> {
    const data: CreateNewPointReq = {
      RoomId: _roomId,
      Username: _username,
      Lat: _latLng.lat,
      Lng: _latLng.lng,
      Description: _description
    };

    const res = await fetch(`${this.p_apiUrl}/api/v1/create-room-point`, {
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

    const res = await fetch(`${this.p_apiUrl}/api/v1/delete-room-point`, {
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
    const res = await fetch(`${this.p_apiUrl}/api/v1/is-room-id-valid?roomId=${_roomId}`, {
      method: "GET"
    });

    return !(res.status === 406);
  }

}
