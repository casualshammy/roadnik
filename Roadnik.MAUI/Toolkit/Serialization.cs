using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Roadnik.MAUI.Toolkit;

internal static class Serialization
{
  private static readonly JsonSerializerSettings p_camelCaseSerializer;

  static Serialization()
  {
    p_camelCaseSerializer = new JsonSerializerSettings()
    {
      ContractResolver = new DefaultContractResolver
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      },
      Formatting = Formatting.Indented
    };
  }

  public static string SerializeToCamelCaseJson(object _obj)
  {
    return JsonConvert.SerializeObject(_obj, p_camelCaseSerializer);
  }

}
