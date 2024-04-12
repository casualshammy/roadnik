using Ax.Fw.DependencyInjection;
using Roadnik.Server.Interfaces;

namespace Roadnik.Server.Modules.HttpClientProvider;

internal class HttpClientProviderImpl : IHttpClientProvider, IAppModule<IHttpClientProvider>
{
  public static IHttpClientProvider ExportInstance(IAppDependencyCtx _ctx) => new HttpClientProviderImpl();

  private HttpClientProviderImpl()
  {
    var handler = new SocketsHttpHandler
    {
      PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    };
    var httpClient = new HttpClient(handler);
    httpClient.Timeout = TimeSpan.FromSeconds(10);
    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
    Value = httpClient;
  }

  public HttpClient Value { get; }

}
