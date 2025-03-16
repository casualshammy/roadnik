using Android.Webkit;
using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Roadnik.MAUI.Interfaces;
using System.Collections.Frozen;
using System.Text.RegularExpressions;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public partial class CachedMauiWebViewClient : MauiWebViewClient
{
  private static readonly FrozenDictionary<string, string> p_corsAllowAllHeaders = new Dictionary<string, string>()
  {
    { "Access-Control-Allow-Origin", "*" }
  }.ToFrozenDictionary();

  private static readonly Regex[] p_cacheRegexes = [
    CacheRegexMapTiles(),
    CacheRegexJs(),
    CacheRegexCss(),
    CacheRegexOsm(),
    GetUnpkgPngRegex(),
    // we can't just cache tracks:
    //   1. at some point request will return 'MoreEntriesAvailable = false', and this info will be cached, thus web app will not request further data
    //   2. tracks wiped by client will remain in cache - we should somehow wipe it...
  ];

  private readonly IWebDataCache? p_webDataCache;
  private readonly ILog? p_log;

  public CachedMauiWebViewClient(
    WebViewHandler _handler,
    IWebDataCache? _tilesCache,
    ILog? _log) : base(_handler)
  {
    p_webDataCache = _tilesCache;
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

    if (GetLocalIndexRegex().IsMatch(url))
    {
      var context = global::Android.App.Application.Context;
      var stream = context.Assets?.Open("webApp/index.html");
      return new WebResourceResponse(MimeTypes.Html, null, 200, "OK", p_corsAllowAllHeaders, stream);
    }

    var localFileMatch = GetLocalFileRegex().Match(url);
    if (localFileMatch.Success)
    {
      var relativePath = localFileMatch.Groups[1].Value;
      var ext = Path.GetExtension(relativePath)?.TrimStart('.');

      var context = global::Android.App.Application.Context;
      var stream = context.Assets?.Open($"webApp/{relativePath}");
      if (MimeLut.MimeLutTable.TryFindMimeTypeByExtension(ext, out var mime))
        return new WebResourceResponse(mime, null, 200, "OK", p_corsAllowAllHeaders, stream);
      else
        return new WebResourceResponse(null, null, 200, "OK", p_corsAllowAllHeaders, stream);
    }

    try
    {
      if (p_webDataCache.TryGetStream(url, out var cachedStream, out var mime))
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

    if (p_cacheRegexes.All(_ => !_.IsMatch(url)))
      return base.ShouldInterceptRequest(_view, _request);

    p_webDataCache.EnqueueDownload(url);
    return base.ShouldInterceptRequest(_view, _request);
  }

  [GeneratedRegex(@"map-tile\?type=[\w\-]+?&x=\d+&y=\d+&z=\d+$")]
  private static partial Regex CacheRegexMapTiles();
  [GeneratedRegex(@"\.js$")]
  private static partial Regex CacheRegexJs();
  [GeneratedRegex(@"\.css$")]
  private static partial Regex CacheRegexCss();
  [GeneratedRegex(@"openstreetmap\.org/\d+/\d+/\d+\.png$")]
  private static partial Regex CacheRegexOsm();
  [GeneratedRegex(@"://unpkg\.com/.+?\.png$")]
  private static partial Regex GetUnpkgPngRegex();

  [GeneratedRegex($@"^https?\://{WEBAPP_HOST}\:?\d*/r/\?id=.+?")]
  private static partial Regex GetLocalIndexRegex();
  [GeneratedRegex($@"^https?\://{WEBAPP_HOST}\:?\d*/r/(.+)$")]
  private static partial Regex GetLocalFileRegex();

}