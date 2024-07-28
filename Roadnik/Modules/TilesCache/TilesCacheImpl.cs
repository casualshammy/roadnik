using Ax.Fw.Cache;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Roadnik.Modules.TilesCache;

internal class TilesCacheImpl : ITilesCache, IAppModule<ITilesCache>
{
  record DownloadTask(string Key, string Url);

  public static ITilesCache ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ISettingsController _settingsController,
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider,
      ILog _log) => new TilesCacheImpl(_settingsController, _lifetime, _httpClientProvider, _log["tiles-cache"]));
  }

  private readonly IRxProperty<FileCache?> p_cacheProp;
  private readonly Subject<DownloadTask> p_downloadTaskSubj = new();

  private TilesCacheImpl(
    ISettingsController _settingsController,
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    ILog _log)
  {
    p_cacheProp = _settingsController.Settings
      .WhereNotNull()
      .Alive(_lifetime, (_conf, _life) =>
      {
        if (_conf.MapTilesCacheSize == null || _conf.MapTilesCacheSize <= 0)
        {
          _log.Warn($"Tiles cache is disabled");
          return null;
        }

        var folder = Path.Combine(_conf.DataDirPath, "tiles-cache");
        var cache = new FileCache(
          _life,
          folder,
          TimeSpan.FromDays(30),
          _conf.MapTilesCacheSize.Value,
          TimeSpan.FromHours(6));

        return cache;
      })
      .ToProperty(_lifetime, null);

    var downloadScheduler = new EventLoopScheduler();
    p_downloadTaskSubj
      .Delay(TimeSpan.FromSeconds(5), downloadScheduler)
      .ObserveOn(downloadScheduler)
      .SelectAsync(async (_task, _ct) =>
      {
        var cache = p_cacheProp.Value;
        if (cache == null)
          return;

        try
        {
          _log.Info($"**Downloading** tile '__{_task.Key}__'...");

          using var res = await _httpClientProvider.Value.GetAsync(_task.Url, _ct);
          res.EnsureSuccessStatusCode();

          using var stream = await res.Content.ReadAsStreamAsync(_ct);
          var mime = res.Content.Headers.ContentType?.ToString();
          await cache.StoreAsync(_task.Key, stream, mime, true, _ct);

          _log.Info($"Tile '__{_task.Key}__' is downloaded");
        }
        catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.NotFound)
        {
          _log.Warn($"Tile '__{_task.Key}__' is not found");
        }
        catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.Unauthorized)
        {
          _log.Warn($"Can't download tile '__{_task.Key}__' - unauthorized");
        }
        catch (Exception ex)
        {
          _log.Warn($"Can't download tile '{_task.Key}': {ex}");
        }
      }, downloadScheduler)
      .Subscribe(_lifetime);
  }

  public void EnqueueUrl(int _x, int _y, int _z, string _type, string _url)
  {
    var key = GetKey(_x, _y, _z, _type);
    p_downloadTaskSubj.OnNext(new DownloadTask(key, _url));
  }

  public Stream? GetOrDefault(int _x, int _y, int _z, string _type)
  {
    var cache = p_cacheProp.Value;
    if (cache == null)
      return null;

    var key = GetKey(_x, _y, _z, _type);
    if (cache.TryGet(key, out var stream, out _))
      return stream;

    return null;
  }

  public bool TryGet(
    int _x,
    int _y,
    int _z,
    string _type,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _hash)
  {
    _stream = null;
    _hash = null;

    var cache = p_cacheProp.Value;
    if (cache == null)
      return false;

    var key = GetKey(_x, _y, _z, _type);
    if (!cache.TryGet(key, out var stream, out var meta))
      return false;

    _stream = stream;
    _hash = meta.Hash;
    return true;
  }

  private static string GetKey(int _x, int _y, int _z, string _type)
  {
    var sb = new StringBuilder();
    sb.Append(_x);
    sb.Append('.');
    sb.Append(_y);
    sb.Append('.');
    sb.Append(_z);
    sb.Append('.');
    sb.Append(_type);
    return sb.ToString();
  }

}
