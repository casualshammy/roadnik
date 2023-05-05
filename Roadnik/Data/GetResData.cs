namespace Roadnik.Data;

internal record GetResData(
  bool Success,
  long LastUpdateUnixMs,
  IEnumerable<StorageEntry> Entries);