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
