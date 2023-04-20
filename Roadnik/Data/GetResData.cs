namespace Roadnik.Data;

internal record GetResData(
  bool Success,
  long? LastUpdateUnixMs,
  StorageEntry? LastEntry,
  IEnumerable<StorageEntry> Entries);