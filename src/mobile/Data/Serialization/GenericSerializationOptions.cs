using System.Text.Json;

namespace Roadnik.MAUI.Data.Serialization;

internal static class GenericSerializationOptions
{
  public static JsonSerializerOptions CaseInsensitive { get; } = new() { PropertyNameCaseInsensitive = true };
}
