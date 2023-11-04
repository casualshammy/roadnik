namespace Roadnik.Data;

internal record GetPathResData(
  long LastUpdateUnixMs,
  bool MoreEntriesAvailable,
  IEnumerable<TimedStorageEntry> Entries);