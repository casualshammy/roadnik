import type { AppId } from "../Guid";

export const SSE_MSG_TYPE_HELLO: string = "ws-msg-hello";
export const SSE_MSG_TYPE_DATA_UPDATED: string = "ws-msg-data-updated";
export const SSE_MSG_PATH_WIPED: string = "ws-msg-path-wiped";
export const SSE_MSG_ROOM_POINTS_UPDATED: string = "ws-msg-room-points-updated";
export const SSE_MSG_PATH_TRUNCATED: string = "ws-msg-path-truncated";

export type SseAbstractMsg = {
  MsgType: string;
}

export type SseMsgHello = SseAbstractMsg & {
  UnixTimeMs: number;
  MaxPathPointsPerRoom: number;
  Timestamps: { [appId: string]: number };
}

export type SseMsgPathWiped = SseAbstractMsg & {
  AppId: AppId;
  UserName: string;
}

export type SseMsgPathTruncated = SseAbstractMsg & {
  AppId: AppId;
  UserName: string;
  PathPoints: number;
}