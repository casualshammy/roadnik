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

namespace Roadnik.Modules.UsersController;

[ExportClass(typeof(IUsersController), Singleton: true)]
internal class UsersControllerImpl : IUsersController
{
  private readonly IDocumentStorage p_storage;

  public UsersControllerImpl(
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
        var entries = await AsyncEnumerable.ToListAsync(
          p_storage.ListSimpleDocumentsAsync<StorageEntry>(_ct: _ct), _ct);

        foreach (var keyGroup in entries.GroupBy(_ => _.Data.Key))
        {
          var limit = _settings.AnonymousMaxPoints;
          var user = await GetUserAsync(keyGroup.Key, _ct);
          if (user != null)
            limit = _settings.RegisteredMaxPoints;

          var counter = 0;
          foreach (var entry in keyGroup.OrderByDescending(_ => _.Created))
            if (++counter > limit)
              await p_storage.DeleteSimpleDocumentAsync<StorageEntry>(entry.Key, _ct);

          var deleted = counter - limit;
          if (deleted > 0)
            log.Warn($"Removed '{deleted}' geo entries of room id '{keyGroup.Key}'");
        }
      }, scheduler)
      .Subscribe(_lifetime);
  }

  public async Task AddUserAsync(string _key, string _email, CancellationToken _ct)
  {
    await p_storage.WriteSimpleDocumentAsync(_key, new User(_key, _email, null), _ct);
  }

  public async Task DeleteUserAsync(string _key, CancellationToken _ct)
  {
    await p_storage.DeleteSimpleDocumentAsync<User>(_key.ToString(), _ct);
  }

  public async Task<User?> GetUserAsync(string _key, CancellationToken _ct)
  {
    var doc = await p_storage.ReadSimpleDocumentAsync<User>(_key, _ct);
    return doc?.Data;
  }

  public async Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken _ct)
  {
    var users = await p_storage
      .ListSimpleDocumentsAsync<User>(_ct: _ct)
      .Select(_ => _.Data)
      .ToListAsync(_ct: _ct);

    return users;
  }

}
