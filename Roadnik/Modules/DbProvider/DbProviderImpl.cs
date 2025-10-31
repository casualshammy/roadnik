using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Interfaces;
using Roadnik.Common.Data;
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
  }

  public IDocumentStorage GenericData { get; }
  public IDocumentStorage Paths { get; }

}
