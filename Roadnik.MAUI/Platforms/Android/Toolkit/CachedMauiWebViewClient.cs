using Android.Webkit;
using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public partial class CachedMauiWebViewClient : MauiWebViewClient
{
  private static readonly FrozenDictionary<string, string> p_corsAllowAllHeaders = new Dictionary<string, string>()
  {
    { "Access-Control-Allow-Origin", "*" }
  }.ToFrozenDictionary();

  private static readonly Regex[] p_cacheRegexes = [
    CacheRegexMapTiles(),
    CacheRegexFavicon(),
    CacheRegexJs(),
    CacheRegexCss(),
    CacheRegexOsm(),
    GetUnpkgPngRegex(),
  ];

  private readonly IWebDataCache? p_webDataCache;
  private readonly IPreferencesStorage? p_storage;
  private readonly ILog? p_log;

  public CachedMauiWebViewClient(
    WebViewHandler _handler,
    IWebDataCache? _tilesCache,
    ILog? _log,
    IPreferencesStorage? _storage) : base(_handler)
  {
    p_webDataCache = _tilesCache;
    p_storage = _storage;
    p_log = _log?["cached-web-view-client"];
  }

  public override WebResourceResponse? ShouldInterceptRequest(
    global::Android.Webkit.WebView? _view,
    IWebResourceRequest? _request)
  {
    if (p_webDataCache == null)
      return base.ShouldInterceptRequest(_view, _request);

    var url = _request?.Url?.ToString();
    if (url == null)
      return base.ShouldInterceptRequest(_view, _request);

    string? overrideCacheKey = null;
    var indexFileMatch = GetIndexRegex().Match(url);
    if (indexFileMatch.Success)
    {
      var version = indexFileMatch.Groups[1].Value;
      overrideCacheKey = $"index-page.{version}";
    }

    try
    {
      if (p_webDataCache.TryGetStream(overrideCacheKey ?? url, out var cachedStream, out var mime))
      {
        if (mime == MimeTypes.Bin)
          mime = null;

        return new WebResourceResponse(mime, null, 200, "OK", p_corsAllowAllHeaders, cachedStream);
      }
    }
    catch (Exception ex)
    {
      p_log?.Error($"Can't get cached resource for url '{url}'", ex);
    }

    if (p_cacheRegexes.All(_ => !_.IsMatch(url)) && !indexFileMatch.Success)
      return base.ShouldInterceptRequest(_view, _request);

    p_webDataCache.EnqueueDownload(url, overrideCacheKey);
    return base.ShouldInterceptRequest(_view, _request);
  }

  [GeneratedRegex(@"map-tile\?type=[\w\-]+?&x=\d+&y=\d+&z=\d+$")]
  private static partial Regex CacheRegexMapTiles();
  [GeneratedRegex(@"favicon\.ico$")]
  private static partial Regex CacheRegexFavicon();
  [GeneratedRegex(@"\.js$")]
  private static partial Regex CacheRegexJs();
  [GeneratedRegex(@"\.css$")]
  private static partial Regex CacheRegexCss();
  [GeneratedRegex(@"openstreetmap\.org/\d+/\d+/\d+\.png$")]
  private static partial Regex CacheRegexOsm();
  [GeneratedRegex(@"://unpkg\.com/.+?\.png$")]
  private static partial Regex GetUnpkgPngRegex();
  [GeneratedRegex(@"/r/\?id=.+?&" + Consts.INDEX_URL_VERSION_QUERY_NAME + @"=(\d+\.\d+\.\d+)")]
  private static partial Regex GetIndexRegex();

}