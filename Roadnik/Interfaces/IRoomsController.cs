using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Common.Data;
using Roadnik.Server.Modules.RoomsController;
using System.Net;

namespace Roadnik.Interfaces;

internal interface IRoomsController
{
  void RegisterRoom(RoomInfo _roomInfo);

  void UnregisterRoom(string _roomId);
  RoomInfo? GetRoom(string _roomId);
  IReadOnlyList<RoomInfo> ListRegisteredRooms();
  void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs);
  void EnqueuePathTruncate(string _roomId, string _username);

  Task<SaveNewPathPointResult> SaveNewPathPointAsync(
    ILog _log,
    IPAddress? _clientIpAddress,
    string _roomId,
    string _username,
    int _sessionId,
    bool _wipeOldPath,
    float _lat,
    float _lng,
    float _alt,
    float? _acc,
    float? _speed,
    float? _battery,
    float? _gsmSignal,
    float? _bearing,
    CancellationToken _ct);
}
