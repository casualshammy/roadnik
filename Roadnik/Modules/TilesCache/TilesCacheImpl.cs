using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Ax.Fw.Cache;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Interfaces;
using Roadnik.Server.Interfaces;
using Roadnik.Server.Toolkit;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Roadnik.Server.Modules.TilesCache;

internal class TilesCacheImpl : ITilesCache, IAppModule<ITilesCache>
{
  record DownloadTask(
    string Key,
    string Url,
    bool IsNkHeadersRequired);

  public static ITilesCache ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IAppConfig _appConfig,
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider,
      ILog _log) => new TilesCacheImpl(_appConfig, _lifetime, _httpClientProvider, _log["tiles-cache"]));
  }

  private readonly FileCache? p_cache;
  private readonly Subject<DownloadTask> p_downloadTaskSubj = new();

  private TilesCacheImpl(
    IAppConfig _appConfig,
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    ILog _log)
  {
    if (_appConfig.MapTilesCacheSize == null || _appConfig.MapTilesCacheSize <= 0)
    {
      _log.Warn($"Tiles cache is disabled");
    }
    else
    {
      p_cache = new FileCache(
        _lifetime,
        Path.Combine(_appConfig.DataDirPath, "tiles-cache"),
        TimeSpan.FromDays(30),
        _appConfig.MapTilesCacheSize.Value,
        TimeSpan.FromHours(6));
    }

    var downloadScheduler = new EventLoopScheduler();
    p_downloadTaskSubj
      .Delay(TimeSpan.FromSeconds(5), downloadScheduler)
      .ObserveOn(downloadScheduler)
      .SelectAsync(async (_task, _ct) =>
      {
        if (p_cache == null)
          return;

        try
        {
          _log.Info($"**Downloading** tile '__{_task.Key}__'...");

          using var httpReq = new HttpRequestMessage(HttpMethod.Get, _task.Url);
          if (_task.IsNkHeadersRequired)
            httpReq.WithNkHeaders();

          using var httpRes = await _httpClientProvider.Value.SendAsync(httpReq, _ct);
          httpRes.EnsureSuccessStatusCode();

          using var pngStream = await httpRes.Content.ReadAsStreamAsync(_ct);

          var mime = httpRes.Content.Headers.ContentType?.ToString();
          await p_cache.StoreAsync(_task.Key, pngStream, mime, true, _ct);

          _log.Info($"Tile '__{_task.Key}__' is downloaded");
        }
        catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.NotFound)
        {
          _log.Warn($"Tile '{_task.Key}' is not found");
        }
        catch (HttpRequestException hex) when (hex.StatusCode == HttpStatusCode.Unauthorized)
        {
          _log.Warn($"Can't download tile '{_task.Key}' - unauthorized");
        }
        catch (Exception ex)
        {
          _log.Warn($"Can't download tile '{_task.Key}': {ex}");
        }
      }, downloadScheduler)
      .Subscribe(_lifetime);
  }

  public void EnqueueUrl(
    int _x,
    int _y,
    int _z,
    string _type,
    string _url,
    bool _isHeaderInjectRequired)
  {
    var key = GetKey(_x, _y, _z, _type);
    p_downloadTaskSubj.OnNext(new DownloadTask(key, _url, _isHeaderInjectRequired));
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

    if (p_cache == null)
      return false;

    var key = GetKey(_x, _y, _z, _type);
    if (!p_cache.TryGet(key, out var stream, out var meta))
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
