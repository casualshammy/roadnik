using Ax.Fw.App.Interfaces;
using Ax.Fw.DependencyInjection;
using Roadnik.Server.Interfaces;
using System.Text.RegularExpressions;

namespace Roadnik.Server.Modules.StravaCredentialsController;

internal interface IStravaCredentialsController { }

internal class StravaCredentialsControllerImpl : IStravaCredentialsController, IAppModule<IStravaCredentialsController>
{
  public static IStravaCredentialsController ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IHttpClientProvider _httpClientProvider)
      => new StravaCredentialsControllerImpl(_httpClientProvider));
  }

  public StravaCredentialsControllerImpl(
    IHttpClientProvider _httpClientProvider)
  {
    //GetCookiesAsync(_httpClientProvider, default);
  }

  public async Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(
    IHttpClientProvider _httpClientProvider,
    CancellationToken _ct)
  {
    using var loginFormReq = new HttpRequestMessage(HttpMethod.Get, "https://www.strava.com/login");
    using var loginFormRes = await _httpClientProvider.HttpClient.SendAsync(loginFormReq, _ct);
    loginFormRes.EnsureSuccessStatusCode();

    var loginFormText = await loginFormRes.Content.ReadAsStringAsync(_ct);
    var authTokenMatch = Regex.Match(
      loginFormText,
      @"name=""authenticity_token"" value=""([^""]+)""");

    if (!authTokenMatch.Success)
      throw new FormatException("Could not acquire login form authenticity token.");

    var authToken = authTokenMatch.Groups[1].Value;

    return new Dictionary<string, string>();
  }

}
