using Ax.Fw.App.Interfaces;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.Server.Interfaces;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.StravaTilesProvider;

internal class StravaTilesProviderImpl : IStravaTilesProvider, IAppModule<IStravaTilesProvider>
{
  public static IStravaTilesProvider ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      ILog _log,
      IHttpClientProvider _httpClientProvider,
      IAppConfig _config) 
      => new StravaTilesProviderImpl(_lifetime, _log["strava-tiles-provider"], _httpClientProvider, _config));
  }

  private const string STRAVA_URL_MAPS = "https://www.strava.com/maps";
  private readonly ConcurrentDictionary<string, string> p_stravaHeaders = [];

  private StravaTilesProviderImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log,
    IHttpClientProvider _httpClientProvider,
    IAppConfig _config)
  {
    Observable
      .Interval(TimeSpan.FromHours(1))
      .Merge(Observable.Timer(TimeSpan.FromSeconds(5)))
      .SelectAsync(async (_, _ct) =>
      {
        try
        {
          var stravaSession = _config.StravaSession;
          if (stravaSession.IsNullOrWhiteSpace())
            return;

          _log.Info("**Refreshing** __Strava token__...");

          using var httpReq = new HttpRequestMessage(HttpMethod.Head, STRAVA_URL_MAPS);
          httpReq.Headers.Add("Cookie", $"_strava4_session={stravaSession}");

          using var httpRes = await _httpClientProvider.HttpClient.SendAsync(httpReq, _ct);
          httpRes.EnsureSuccessStatusCode();

          var cookies = ParseSetCookieHeader(httpRes);
          var expCookie = cookies.GetValueOrDefault("_strava_CloudFront-Expires");
          if (expCookie.IsNullOrWhiteSpace() || !long.TryParse(expCookie, out var expUnix))
            throw new InvalidDataException($"Failed to parse Strava token expiration from response cookies ({expCookie})");

          foreach (var (key, value) in cookies)
            p_stravaHeaders.AddOrUpdate(key, value, (_, _) => value);

          var expDateTime = DateTimeOffset.FromUnixTimeMilliseconds(expUnix);

          _log.Info($"__Strava token__ **refreshed** successfully (__{expDateTime}__)");
        }
        catch (Exception ex)
        {
          _log.Error($"Failed to refresh Strava token: {ex}");
        }
      })
      .Subscribe(_lifetime);
  }

  public IReadOnlyDictionary<string, string> Headers => p_stravaHeaders;

  private static IReadOnlyDictionary<string, string> ParseSetCookieHeader(HttpResponseMessage _msg)
  {
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!_msg.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
      return dict;

    foreach (var setCookieHeader in setCookieHeaders)
    {
      var valuePart = setCookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
      if (valuePart.IsNullOrWhiteSpace())
        continue;

      var kvp = valuePart.Split('=');
      if (kvp.Length == 2)
      {
        var key = kvp[0].Trim();
        var value = kvp[1].Trim();
        if (!key.IsNullOrWhiteSpace() && !value.IsNullOrWhiteSpace())
          dict[key] = value;
      }
    }

    return dict;
  }

}