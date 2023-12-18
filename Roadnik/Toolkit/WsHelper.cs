using Roadnik.Server.Data.WebSockets;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roadnik.Server.Toolkit;

public static class WsHelper
{
  private static ImmutableDictionary<string, Type> p_wsMsgTypes = ImmutableDictionary<string, Type>.Empty;
  private static JsonSerializerContext? p_jsonSerializerContext = null;

  static WsHelper()
  {

  }

  public static void RegisterMsType(string _msgTypeSlug, Type _msgType)
  {
    p_wsMsgTypes = p_wsMsgTypes.SetItem(_msgTypeSlug, _msgType);
  }

  public static void RegisterSerializationContext(JsonSerializerContext _jsonSerializerContext)
  {
    p_jsonSerializerContext = _jsonSerializerContext;
  }

  public static object? ParseWsMessage(byte[] _msgBuffer, int _length)
  {
    var json = Encoding.UTF8.GetString(_msgBuffer, 0, _length);
    return ParseWsMessage(json);
  }

  public static object? ParseWsMessage(string _json)
  {
    if (p_jsonSerializerContext == null)
      throw new InvalidOperationException("You must register serialization context by calling 'RegisterSerializationContext' method");

    try
    {
      if (JsonSerializer.Deserialize(_json, typeof(WsBaseMsg), p_jsonSerializerContext) is not WsBaseMsg incomingBaseMsg)
        return null;

      if (!p_wsMsgTypes.TryGetValue(incomingBaseMsg.Type, out var type))
        return null;

      return ((JsonElement)incomingBaseMsg.Payload).Deserialize(type, p_jsonSerializerContext);
    }
    catch
    {
      return null;
    }
  }

  public static byte[] CreateWsMessage<T>(T _msg) where T : notnull
  {
    if (p_jsonSerializerContext == null)
      throw new InvalidOperationException("You must register serialization context by calling 'RegisterSerializationContext' method");

    var type = typeof(T);
    if (!p_wsMsgTypes.Values.Contains(type))
      throw new InvalidOperationException($"Can't serialize object of type '{type.Name}'!");

    var wsMsgTypeInfo = p_wsMsgTypes.First(_ => _.Value == type);

    var baseMsg = new WsBaseMsg(wsMsgTypeInfo.Key, _msg);
    var json = JsonSerializer.Serialize(baseMsg, typeof(WsBaseMsg), p_jsonSerializerContext);
    return Encoding.UTF8.GetBytes(json);
  }

}
