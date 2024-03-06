using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roadnik.Server.Toolkit;

public abstract class GenericController
{
  public abstract void RegisterPaths(WebApplication _app);
  public abstract Task<bool> AuthAsync(HttpRequest _req, CancellationToken _ct);

  protected static IResult Forbidden(string _details) => Results.Problem(_details, statusCode: 403);
  protected static IResult InternalServerError(string? _details = null) => Results.Problem(_details, statusCode: (int)HttpStatusCode.InternalServerError);
  protected static IResult BadRequest(string _details) => Results.BadRequest(_details);
  protected static IResult Json<T>(T _obj, JsonSerializerContext _jsonCtx) where T : notnull
  {
    var json = JsonSerializer.Serialize(_obj, typeof(T), _jsonCtx);
    return Results.Content(json, MimeMapping.KnownMimeTypes.Json, Encoding.UTF8);
  }

}
