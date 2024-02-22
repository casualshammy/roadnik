using Ax.Fw.DependencyInjection;
using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Modules.HttpClientProvider;

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
    httpClient.DefaultRequestHeaders.Add("User-Agent", $"{ReqResUtil.UserAgent}/{AppInfo.Current.VersionString}");
    Value = httpClient;
  }

  public HttpClient Value { get; }

}
