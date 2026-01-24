using Ax.Fw.App.Interfaces;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Interfaces;
using System.Reactive.Linq;

namespace Roadnik.Server.Toolkit;

internal class StravaTokenRefresher
{
  public StravaTokenRefresher(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IHttpClientProvider _httpClientProvider,
    IAppConfig _appConfig)
  {
    Observable
      .Interval(TimeSpan.FromHours(1))
      .Merge(Observable.Timer(TimeSpan.FromSeconds(60)))
      .SelectAsync(async (_, _ct) =>
      {
        try
        {
          var url = _appConfig.StravaTilesRideUrl?.Replace("%z%", "9").Replace("%x%", "260").Replace("%y%", "181");
          if (url.IsNullOrWhiteSpace())
            return;

          _log.Info("**Refreshing** __Strava token__...");
          using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
          foreach (var (headerName, headerValue) in _appConfig.StravaTilesHeaders)
            httpReq.Headers.Add(headerName, headerValue);

          using var httpRes = await _httpClientProvider.HttpClient.SendAsync(httpReq, _ct);
          httpRes.EnsureSuccessStatusCode();

          _log.Info($"__Strava token__ **refreshed successfully** (content length: {httpRes.Content.Headers.ContentLength})");
        }
        catch (Exception ex)
        {
          _log.Error($"Failed to refresh Strava token: {ex}");
        }
      })
      .Subscribe(_lifetime);
  }
}
