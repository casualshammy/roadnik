using Ax.Fw.Attributes;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Modules.HttpClientProvider;

[ExportClass(typeof(IHttpClientProvider), Singleton: true)]
internal class HttpClientProviderImpl : IHttpClientProvider
{
  private readonly HttpClient p_httpClient = new();

  public HttpClientProviderImpl()
  {
    p_httpClient.Timeout = TimeSpan.FromSeconds(10);
    p_httpClient.DefaultRequestHeaders.Add("User-Agent", $"RoadnikApp/{AppInfo.Current.VersionString}");
    Value = p_httpClient;
  }

  public HttpClient Value { get; }

}
