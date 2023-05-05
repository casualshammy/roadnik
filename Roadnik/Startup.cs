using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Roadnik.Toolkit;

namespace Roadnik;

public class Startup
{
  public Startup(IConfiguration _configuration)
  {
    Configuration = _configuration;
  }

  public IConfiguration Configuration { get; }

  // This method gets called by the runtime. Use this method to add services to the container.
  public void ConfigureServices(IServiceCollection _services)
  {
    _services.AddControllers(_options => _options.Filters.Add<ApiKeyFilter>());
    _services.AddRazorPages();
  }

  // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
  public void Configure(IApplicationBuilder _app, IWebHostEnvironment _env)
  {
    if (_env.IsDevelopment())
      _app.UseDeveloperExceptionPage();

    _app
      .UseRouting()
      .UseWebSockets(new WebSocketOptions()
      {
        KeepAliveInterval = TimeSpan.FromSeconds(300)
      })
      .UseResponseCompression()
      .UseEndpoints(_endpoints => _endpoints.MapControllers());
  }
}
