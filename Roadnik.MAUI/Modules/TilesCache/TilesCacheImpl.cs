﻿using Ax.Fw.Attributes;
using Ax.Fw.Cache;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using JustLogger;
using JustLogger.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Roadnik.MAUI.Modules.TilesCache;

[ExportClass(typeof(ITilesCache), Singleton: true)]
internal class TilesCacheImpl : ITilesCache
{
  private readonly Subject<string> p_workFlow = new();
  private readonly ILogger p_log;

  public TilesCacheImpl(
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    ILogger _logger)
  {
    p_log = _logger["tiles-cache"];

    var cacheDir = Path.Combine(FileSystem.Current.CacheDirectory, "tiles-cache");
    var cache = new FileCache(_lifetime, cacheDir, TimeSpan.FromDays(1), 50 * 1024 * 1024, null);
    cache.CleanFiles();
    Cache = cache;

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
        if (cache.IsKeyExists(_url, out _))
          return;

        try
        {
          using (var networkStream = await _httpClientProvider.Value.GetStreamAsync(_url, _ct))
            await cache.StoreAsync(_url, networkStream, _ct);

          p_log.Info($"Url is downloaded: '{_url}'");
        }
        catch (Exception ex)
        {
          p_log.Error($"Can't download tile '{_url}'", ex);
        }
      }, scheduler)
      .Do(_ => Interlocked.Decrement(ref workCounter))
      .Subscribe(_lifetime);
  }

  public FileCache Cache { get; }

  public void EnqueueDownload(string _url)
  {
    p_log.Info($"New url for downloading: '{_url}'");
    p_workFlow.OnNext(_url);
  }
}
