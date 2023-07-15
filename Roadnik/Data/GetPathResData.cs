namespace Roadnik.Data;

internal record GetPathResData(
  long LastUpdateUnixMs,
  IEnumerable<TimedStorageEntry> Entries);