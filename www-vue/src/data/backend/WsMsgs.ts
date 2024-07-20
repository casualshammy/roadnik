export type WsBaseMsg = {
  Type: string;
  Payload: any;
}

export type WsMsgHello = {
  UnixTimeMs: number;
  MaxPathPointsPerRoom: number;
}

export type WsMsgPathWiped = {
  Username: string;
}

export type WsMsgPathTruncated = {
  Username: string;
  PathPoints: number;
}