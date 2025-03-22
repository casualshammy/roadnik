using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roadnik.Server.Toolkit;

internal abstract class GenericController
{
  private readonly JsonSerializerContext p_jsonCtx;

  public GenericController(string _route, JsonSerializerContext _jsonCtx)
  {
    Route = _route.TrimStart('/');
    p_jsonCtx = _jsonCtx;
  }

  public string Route { get; }

  public abstract void RegisterPaths(WebApplication _app);
  public abstract Task<bool> AuthAsync(HttpRequest _req, CancellationToken _ct);

  protected static IResult Forbidden(string _details) => Results.Problem(_details, statusCode: 403);
  protected static IResult InternalServerError(string? _details = null) => Results.Problem(_details, statusCode: (int)HttpStatusCode.InternalServerError);
  protected static IResult BadRequest(string _details) => Results.BadRequest(_details);
  protected static IResult Problem(HttpStatusCode _code, string? _details) => Results.Problem(_details, statusCode: (int)_code);
  protected IResult Json<T>(T _obj) where T : notnull
  {
    var json = JsonSerializer.Serialize(_obj, typeof(T), p_jsonCtx);
    return Results.Content(json, Ax.Fw.MimeTypes.Json, Encoding.UTF8);
  }
  protected static IResult NotFound() => Results.NotFound();

}
