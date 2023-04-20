namespace Roadnik.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class WebSocketMsgAttribute : Attribute
{
  public WebSocketMsgAttribute(string _type)
  {
    Type = _type;
  }

  public string Type { get; }
}
