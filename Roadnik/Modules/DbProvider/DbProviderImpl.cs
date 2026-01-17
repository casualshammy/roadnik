using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Roadnik.Server.Data;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.DbProvider;

internal class DbProviderImpl : IDbProvider
{
  public DbProviderImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IAppConfig _appConfig)
  {
    GenericData = _lifetime.ToDisposeOnEnding(new SqliteDocumentStorage(
      Path.Combine(_appConfig.DataDirPath, "data.v1.db"),
      DocStorageJsonCtx.Default,
      new StorageCacheOptions(1000, TimeSpan.FromHours(1))));

    Paths = _lifetime.ToDisposeOnEnding(new SqliteDocumentStorage(
      Path.Combine(_appConfig.DataDirPath, "paths.v1.db"),
      DocStorageJsonCtx.Default,
      new StorageCacheOptions(1000, TimeSpan.FromHours(1))));

    Tiles = _lifetime.ToDisposeOnEnding(new SqliteBlobStorage(
      Path.Combine(_appConfig.DataDirPath, "tiles.v0.db"),
      new StorageRetentionOptions(
        [
          new StorageRetentionRule(Consts.TILE_TYPE_STRAVA_HEATMAP_RIDE, TimeSpan.FromDays(365), null),
          new StorageRetentionRule(Consts.TILE_TYPE_STRAVA_HEATMAP_RUN, TimeSpan.FromDays(365), null),
          new StorageRetentionRule(Consts.TILE_TYPE_TF_OPENCYCLEMAP, TimeSpan.FromDays(30), null),
          new StorageRetentionRule(Consts.TILE_TYPE_TF_OUTDOORS, TimeSpan.FromDays(30), null),
          new StorageRetentionRule(Consts.TILE_TYPE_TF_TRANSPORT, TimeSpan.FromDays(30), null),
          new StorageRetentionRule(Consts.TILE_TYPE_CARTO_DARK, TimeSpan.FromDays(30), null)
        ],
        TimeSpan.FromDays(7),
        _docs =>
        {
          foreach (var group in _docs.GroupBy(_ => _.Namespace))
            _log.Info($"**Removed** __{group.Count()}__ **old tiles** from ns __'{group.Key}'__");
        })));

    Observable
      .Interval(TimeSpan.FromHours(6))
      .StartWithDefault()
      .Subscribe(_ =>
      {
        try
        {
          GenericData.Flush(true);
          Paths.Flush(true);
          Tiles.Flush(true);
        }
        catch (Exception ex)
        {
          _log.Error($"Error occured while trying to flush: {ex}");
        }
      }, _lifetime);
  }

  public IDocumentStorage GenericData { get; }
  public IDocumentStorage Paths { get; }
  public IBlobStorage Tiles { get; }

}
