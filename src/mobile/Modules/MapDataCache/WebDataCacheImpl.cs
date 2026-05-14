using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Storage;
using Ax.Fw.Storage.Data;
using Ax.Fw.Storage.Data.Retention;
using Ax.Fw.Storage.Interfaces;
using Roadnik.MAUI.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Roadnik.MAUI.Modules.MapDataCache;

internal class WebDataCacheImpl : IWebDataCache, IAppModule<IWebDataCache>
{
  public static IWebDataCache ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      IHttpClientProvider _httpClientProvider,
      ILog _logger) => new WebDataCacheImpl(_lifetime, _httpClientProvider, _logger["web-data-cache"]));
  }

  private const string DEFAULT_NS = "default";
  private const int HEADER_SIZE = 128;
  private readonly SqliteBlobStorage p_storage;
  private readonly Subject<string> p_workFlow = new();
  private readonly ILog p_log;

  private WebDataCacheImpl(
    IReadOnlyLifetime _lifetime,
    IHttpClientProvider _httpClientProvider,
    ILog _logger)
  {
    p_log = _logger;

    p_storage = _lifetime.ToDisposeOnEnded(new SqliteBlobStorage(
      Path.Combine(FileSystem.Current.CacheDirectory, "web-data-cache.db"), 
      new StorageRetentionOptions( 
        [
          new StorageRetentionRuleAge(DEFAULT_NS, null, TimeSpan.FromDays(30), null),
          new StorageRetentionRuleTotalSize(DEFAULT_NS, null, 100 * 1024 * 1024)
        ], 
        TimeSpan.FromHours(1), 
        _deletedDocs =>
        {
          p_log.Info($"Deleted {_deletedDocs.Count} documents from cache");
        })));

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
        var keyExist = p_storage
          .ListBlobsMeta(DEFAULT_NS, new LikeExpr(_url))
          .Any();

        if (keyExist)
          return;

        try
        {
          using var res = await _httpClientProvider.Value.GetAsync(_url, _ct);
          res.EnsureSuccessStatusCode();

          var mime = res.Content.Headers.ContentType?.ToString();
          var mimeHeader = new byte[HEADER_SIZE];
          Encoding.UTF8.GetBytes(mime ?? Ax.Fw.MimeTypes.Bin.Mime, mimeHeader);

          using var ms = new MemoryStream();
          await ms.WriteAsync(mimeHeader, _ct);

          using (var stream = await res.Content.ReadAsStreamAsync(_ct))
            await stream.CopyToAsync(ms, _ct);

          ms.Position = 0;
          await p_storage.WriteBlobAsync(DEFAULT_NS, _url, ms, ms.Length, _ct);

          p_log.Info($"**Url is downloaded**: '__{_url}__'");
        }
        catch (Exception ex)
        {
          p_log.Error($"Can't download '{_url}'", ex);
        }
      }, scheduler)
      .Do(_ => Interlocked.Decrement(ref workCounter))
      .Subscribe(_lifetime);
  }

  public IBlobStorage Cache => p_storage;

  public void EnqueueDownload(
    string _url)
  {
    p_log.Info($"**New url for downloading**: '__{_url}__'");
    p_workFlow.OnNext(_url);
  }

  public bool TryGetStream(
    string _url,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _mime)
  {
    if (p_storage.TryReadBlob(DEFAULT_NS, _url, out BlobStream? stream, out _))
    {
      var mimeHeader = new byte[HEADER_SIZE];
      stream.ReadExactly(mimeHeader);
      var mime = Encoding.UTF8.GetString(mimeHeader);

      _stream = stream;
      _mime = mime;
      return true;
    }

    _stream = null;
    _mime = null;
    return false;
  }

}
