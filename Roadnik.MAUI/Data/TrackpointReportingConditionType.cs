using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Roadnik.MAUI.Data;

[JsonConverter(typeof(StringEnumConverter))]
internal enum TrackpointReportingConditionType
{
  TimeAndDistance = 1,
  TimeOrDistance
}
