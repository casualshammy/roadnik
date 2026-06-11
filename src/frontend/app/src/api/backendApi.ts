import type { GetPathResData, SseAbstractMsg } from '@/data/backend';
import type { AppId } from '@/data/Guid';
import { sleepAsync } from '@/toolkit/commonToolkit';
import { LatLng } from 'leaflet';
import { Observable, ReplaySubject, Subject } from 'rxjs';

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
  AppId: AppId | null;
  RoomId: string;
  Username: string;
  Lat: number;
  Lng: number;
  Description: string;
}

export class BackendApi {
  private readonly p_apiUrl: string;
  private readonly p_eventSubj = new ReplaySubject<SseAbstractMsg>(5);

  constructor(_apiUrl: string) {
    this.p_apiUrl = _apiUrl;
  }

  public get events(): Observable<SseAbstractMsg> {
    return this.p_eventSubj;
  }

  public setupEventSource(
    _roomId: string
  ): void {
    console.log(`Setting up EventSource...`);
    const url = `${this.p_apiUrl}/api/v1/events?roomId=${_roomId}`;
    const eventSource = new EventSource(url);

    eventSource.onmessage = _ev => {
      const rawMsg = JSON.parse(_ev.data);
      if (rawMsg.MsgType === undefined)
        return;

      const msg: SseAbstractMsg = rawMsg;
      console.log(`EventSource message: ${msg.MsgType}`);
      this.p_eventSubj.next(msg);
    }

    eventSource.onerror = _ev => {
      console.error("EventSource error:", _ev);
      eventSource.close();
      sleepAsync(1000).then(() => {
        this.setupEventSource(_roomId);
      });
    };
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
      AppId: null,
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
