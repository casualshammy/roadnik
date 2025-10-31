import type { AppId } from "../Guid";

export type WsBaseMsg = {
  Type: string;
  Payload: any;
}

export type WsMsgHello = {
  UnixTimeMs: number;
  MaxPathPointsPerRoom: number;
  Timestamps: { [appId: string]: number };
}

export type WsMsgPathWiped = {
  AppId: AppId;
  UserName: string;
}

export type WsMsgPathTruncated = {
  AppId: AppId;
  UserName: string;
  PathPoints: number;
}