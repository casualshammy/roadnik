using Roadnik.Common.Data;

namespace Roadnik.Common.ReqRes;

public record GetPathResData(
  long LastUpdateUnixMs,
  bool MoreEntriesAvailable,
  IEnumerable<TimedStorageEntry> Entries);