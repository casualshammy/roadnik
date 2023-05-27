using Roadnik.Data;

namespace Roadnik.Interfaces;

public interface IRoomsController
{
  Task RegisterRoomAsync(string _roomId, string _email, CancellationToken _ct);
  Task UnregisterRoomAsync(string _roomId, CancellationToken _ct);
  Task<User?> GetRoomAsync(string _roomId, CancellationToken _ct);
  Task<IReadOnlyList<User>> ListRegisteredRoomsAsync(CancellationToken _ct);
  void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs);
}
