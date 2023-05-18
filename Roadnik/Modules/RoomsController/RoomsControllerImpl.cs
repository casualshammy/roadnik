using Ax.Fw;
using Ax.Fw.Attributes;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage.Interfaces;
using JustLogger.Interfaces;
using Roadnik.Data;
using Roadnik.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Roadnik.Modules.RoomsController;

[ExportClass(typeof(IRoomsController), Singleton: true)]
internal class RoomsControllerImpl : IRoomsController
{
  private readonly IDocumentStorage p_storage;

  public RoomsControllerImpl(
    IDocumentStorage _storage,
    IReadOnlyLifetime _lifetime,
    ISettings _settings,
    ILogger _log)
  {
    p_storage = _storage;
    var log = _log["users-controller"];

    _lifetime.DisposeOnCompleted(Pool<EventLoopScheduler>.Get(out var scheduler));

    Observable
      .Interval(TimeSpan.FromHours(1), scheduler)
      .StartWithDefault()
      .Delay(TimeSpan.FromMinutes(10), scheduler)
      .ObserveOn(scheduler)
      .SelectAsync(async (_, _ct) =>
      {
        var entries = p_storage.ListSimpleDocumentsAsync<StorageEntry>(_ct: _ct);

        await foreach (var roomGroup in entries.GroupBy(_ => _.Data.RoomId))
        {
          var limit = _settings.AnonymousMaxPoints;
          var user = await GetRoomAsync(roomGroup.Key, _ct);
          if (user != null)
            limit = _settings.RegisteredMaxPoints;

          var counter = 0;
          await foreach (var entry in roomGroup.OrderByDescending(_ => _.Created))
            if (++counter > limit)
              await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

          var deleted = counter - limit;
          if (deleted > 0)
            log.Warn($"Removed '{deleted}' geo entries of room id '{roomGroup.Key}'");
        }
      }, scheduler)
      .Subscribe(_lifetime);
  }

  public async Task RegisterRoomAsync(string _roomId, string _email, CancellationToken _ct)
  {
    await p_storage.WriteSimpleDocumentAsync(_roomId, new User(_roomId, _email, null), _ct);
  }

  public async Task UnregisterRoomAsync(string _roomId, CancellationToken _ct)
  {
    await p_storage.DeleteSimpleDocumentAsync<User>(_roomId.ToString(), _ct);
  }

  public async Task<User?> GetRoomAsync(string _roomId, CancellationToken _ct)
  {
    var doc = await p_storage.ReadSimpleDocumentAsync<User>(_roomId, _ct);
    return doc?.Data;
  }

  public async Task<IReadOnlyList<User>> ListRegisteredRoomsAsync(CancellationToken _ct)
  {
    var users = await p_storage
      .ListSimpleDocumentsAsync<User>(_ct: _ct)
      .Select(_ => _.Data)
      .ToListAsync(_ct: _ct);

    return users;
  }

}
