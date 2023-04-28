using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace Roadnik.Toolkit;

public class JsonNetController : Controller
{
  public ActionResult Json(object _data, bool _humanReadable = false)
  {
    var json = JsonConvert.SerializeObject(_data, _humanReadable ? Formatting.Indented : Formatting.None);
    return Content(json, MimeMapping.KnownMimeTypes.Json, Encoding.UTF8);
  }

  public async Task<T?> GetJsonRequest<T>()
  {
    using var sr = new StreamReader(Request.Body);
    var json = await sr.ReadToEndAsync();
    try
    {
      return JsonConvert.DeserializeObject<T>(json);
    }
    catch (Exception)
    {
      return default;
    }
  }

  public ActionResult Forbidden(object _data)
  {
    return StatusCode(403, _data);
  }

  public ActionResult Forbidden(string? _reason = null)
  {
    return StatusCode(403, _reason);
  }
}
