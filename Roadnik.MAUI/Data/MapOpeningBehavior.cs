using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Roadnik.MAUI.Data;

[JsonConverter(typeof(StringEnumConverter))]
internal enum MapOpeningBehavior
{
  LastPosition = 1,
  AllTracks,
  LastTrackedRoute
}