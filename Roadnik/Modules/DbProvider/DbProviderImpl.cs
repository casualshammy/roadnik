using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Data.Settings;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.DbProvider;

internal class DbProviderImpl : IDbProvider
{
  public DbProviderImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    RawAppSettings _rawAppSettings)
  {
    GenericData = _lifetime.ToDisposeOnEnding(new SqliteDocumentStorage(
      Path.Combine(_rawAppSettings.DataDirPath, "data.v0.db"),
      DocStorageJsonCtx.Default,
      new StorageCacheOptions(1000, TimeSpan.FromHours(1))));

    Paths = _lifetime.ToDisposeOnEnding(new SqliteDocumentStorage(
      Path.Combine(_rawAppSettings.DataDirPath, "paths.v0.db"),
      DocStorageJsonCtx.Default,
      new StorageCacheOptions(1000, TimeSpan.FromHours(1))));

    Observable
      .Interval(TimeSpan.FromHours(6))
      .StartWithDefault()
      .Subscribe(_ =>
      {
        try
        {
          GenericData.Flush(true);
          Paths.Flush(true);
        }
        catch (Exception ex)
        {
          _log.Error($"Error occured while trying to flush: {ex}");
        }
      }, _lifetime);

    // migration
    Observable
      .Return(Unit.Default)
      .Subscribe(_ =>
      {
        try
        {
          var convertedCount = 0;
          foreach (var entry in GenericData.ListDocuments<StorageEntry>("geo-data"))
          {
            var split = entry.Key.Split('.');
            if (split.Length != 2)
              continue;

            var roomId = split[0];
            var timestamp = split[1];
            Paths.WriteDocument(roomId, timestamp, entry);
            ++convertedCount;
          }
          if (convertedCount > 0)
            _log.Warn($"{convertedCount} path entries were transferred");
        }
        catch (Exception ex)
        {
          _log.Error($"Error occured while trying to transfer path entries: {ex}");
        }
      }, _lifetime);
  }

  public IDocumentStorage GenericData { get; }
  public IDocumentStorage Paths { get; }

}
