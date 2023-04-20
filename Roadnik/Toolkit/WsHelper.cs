using Ax.Fw;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Roadnik.Attributes;
using Roadnik.Data;
using System.Text;

namespace Roadnik.Toolkit;

public static class WsHelper
{
  private static readonly IReadOnlyDictionary<string, Type> p_msgTypes;

  static WsHelper()
  {
    var typesCache = new Dictionary<string, Type>();
    foreach (var type in Utilities.GetTypesWithAttr<WebSocketMsgAttribute>(true))
    {
      var attr = GetAttribute<WebSocketMsgAttribute>(type);
      if (attr is not null)
        typesCache.Add(attr.Type, type);
    }

    p_msgTypes = typesCache;
  }

  public static object? ParseWsMessage(byte[] _msgBuffer, int _length)
  {
    var json = Encoding.UTF8.GetString(_msgBuffer, 0, _length);
    return ParseWsMessage(json);
  }

  public static object? ParseWsMessage(string _json)
  {
    var incomingBaseMsg = JsonConvert.DeserializeObject<WsBaseMsg>(_json);

    if (incomingBaseMsg is null)
      return null;

    if (!p_msgTypes.TryGetValue(incomingBaseMsg.Type, out var type))
      return null;

    var msg = incomingBaseMsg.Payload.ToObject(type);
    if (msg is null)
      return null;

    return msg;
  }

  public static byte[] CreateWsMessage<T>(T _msg) where T : notnull
  {
    var attr = GetAttribute<WebSocketMsgAttribute>(typeof(T));
    if (attr is null)
      throw new FormatException($"Data object must have '{nameof(WebSocketMsgAttribute)}' attribute!");

    var baseMsg = new WsBaseMsg(attr.Type, JToken.FromObject(_msg));
    var json = JsonConvert.SerializeObject(baseMsg);
    var buffer = Encoding.UTF8.GetBytes(json);
    return buffer;
  }

  private static T? GetAttribute<T>(Type _type) where T : Attribute
  {
    var attr = Attribute.GetCustomAttribute(_type, typeof(T)) as T;
    return attr;
  }

}
