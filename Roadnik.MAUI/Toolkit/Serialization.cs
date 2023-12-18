using System.Text.Json;

namespace Roadnik.MAUI.Toolkit;

internal static class Serialization
{
  private static readonly JsonSerializerOptions p_camelCaseSerializer;

  static Serialization()
  {
    p_camelCaseSerializer = new JsonSerializerOptions()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true,
    };
  }

  public static string SerializeToCamelCaseJson(object? _obj) => JsonSerializer.Serialize(_obj, p_camelCaseSerializer);

}
