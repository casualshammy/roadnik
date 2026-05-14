import type { AppId } from "../Guid";

export interface TimedStorageEntry {
  AppId: AppId;
  UnixTimeMs: number;
  Username: string;
  Latitude: number;
  Longitude: number;
  Altitude: number;
  Speed?: number | null;
  Accuracy?: number | null;
  Battery?: number | null;
  GsmSignal?: number | null;
  Bearing?: number | null;
  HR?: number | null;
}
