using Ax.Fw;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roadnik.Server.Toolkit;

internal abstract class GenericController
{
  private readonly JsonSerializerContext p_jsonCtx;

  public GenericController(JsonSerializerContext _jsonCtx)
  {
    p_jsonCtx = _jsonCtx;
  }

  public abstract void RegisterPaths(WebApplication _app);

  protected static IResult Forbidden(string _details) => Results.Problem(_details, statusCode: 403);
  protected static IResult InternalServerError(string? _details = null) => Results.Problem(_details, statusCode: (int)HttpStatusCode.InternalServerError);
  protected static IResult BadRequest(string _details) => Results.BadRequest(_details);
  protected static IResult Problem(HttpStatusCode _code, string? _details) => Results.Problem(_details, statusCode: (int)_code);
  protected IResult Json<T>(T _obj) where T : notnull
  {
    var json = JsonSerializer.Serialize(_obj, typeof(T), p_jsonCtx);
    return Results.Content(json, MimeTypes.Json.Mime, Encoding.UTF8);
  }
  protected static IResult NotFound() => Results.NotFound();

}
