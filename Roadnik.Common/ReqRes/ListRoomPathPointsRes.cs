using Roadnik.Common.Data;

namespace Roadnik.Common.ReqRes;

public record ListRoomPathPointsRes(
  long LastUpdateUnixMs,
  bool MoreEntriesAvailable,
  IEnumerable<TimedStorageEntry> Entries);