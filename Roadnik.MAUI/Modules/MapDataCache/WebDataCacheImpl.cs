using Ax.Fw.Cache;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.MapDataCache;

internal class WebDataCacheImpl : IWebDataCache, IAppModule<IWebDataCache>
{
  public static IWebDataCache ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider,
      ILog _logger) => new WebDataCacheImpl(_lifetime, _httpClientProvider, _logger));
  }

  private readonly Subject<string> p_workFlow = new();
  private readonly ILog p_log;

  private WebDataCacheImpl(
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    ILog _logger)
  {
    p_log = _logger["tiles-cache"];

    var cacheDir = Path.Combine(FileSystem.Current.CacheDirectory, "tiles-cache");
    Directory.CreateDirectory(cacheDir);

    var cache = new FileCache(
      _lifetime,
      cacheDir,
      TimeSpan.FromDays(3),
      50 * 1024 * 1024,
      TimeSpan.FromHours(1));

#if DEBUG
    cache.RequestCleanFiles();
#endif

    var scheduler = new EventLoopScheduler();
    var workCounter = 0;

    p_workFlow
      .Do(_ =>
      {
        var workRemain = Interlocked.Increment(ref workCounter);
        if (workRemain > 100)
          p_log.Warn($"Work in queue: '{workRemain}'");
      })
      .ObserveOn(scheduler)
      .SelectAsync(async (_url, _ct) =>
      {
        if (_url == null)
          return;
        if (cache.IsKeyExists(_url, out _, out _))
          return;

        try
        {
          using var res = await _httpClientProvider.Value.GetAsync(_url, _ct);
          res.EnsureSuccessStatusCode();

          using var stream = await res.Content.ReadAsStreamAsync(_ct);
          var mime = res.Content.Headers.ContentType?.ToString();
          await cache.StoreAsync(_url, stream, mime, true, _ct);

          p_log.Info($"Url is downloaded: '{_url}'");
        }
        catch (Exception ex)
        {
          p_log.Error($"Can't download '{_url}'", ex);
        }
      }, scheduler)
      .Do(_ => Interlocked.Decrement(ref workCounter))
      .Subscribe(_lifetime);

    Cache = cache;
  }

  public FileCache Cache { get; }

  public void EnqueueDownload(string _url)
  {
    p_log.Info($"New url for downloading: '{_url}'");
    p_workFlow.OnNext(_url);
  }

  public bool TryGetStream(
    string _url,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _mime)
  {
    _stream = null;
    _mime = null;

    if (Cache.TryGet(_url, out var stream, out var meta))
    {
      _stream = stream;
      _mime = meta.Mime;
      return true;
    }

    return false;
  }

}
