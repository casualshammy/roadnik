import type { TimedStorageEntry } from ".";

export type GetPathResData = {
  LastUpdateUnixMs: number;
  MoreEntriesAvailable: boolean;
  Entries: TimedStorageEntry[];
}