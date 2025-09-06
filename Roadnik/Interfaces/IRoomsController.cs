using Roadnik.Common.Data;

namespace Roadnik.Server.Interfaces;

internal interface IRoomsController
{
  void RegisterRoom(RoomInfo _roomInfo);

  void UnregisterRoom(string _roomId);
  RoomInfo? GetRoom(string _roomId);
  IReadOnlyList<RoomInfo> ListRegisteredRooms();
  void EnqueueUserWipe(
    string _roomId,
    Guid _appInstanceId,
    string _userName,
    long _upToDateTimeUnixMs);

  void EnqueuePathTruncate(
    string _roomId, 
    Guid _appInstanceId,
    string _userName);

}
