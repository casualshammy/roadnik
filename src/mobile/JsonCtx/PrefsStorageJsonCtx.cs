using Roadnik.MAUI.Data;
using Roadnik.MAUI.Data.LocationProvider;
using System.Text.Json.Serialization;

namespace Roadnik.MAUI.JsonCtx;

[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(TrackpointReportingConditionType))]
[JsonSerializable(typeof(LocationProviders))]
[JsonSerializable(typeof(HrmDeviceInfo))]
[JsonSerializable(typeof(IReadOnlyList<BookmarkEntry>))]
internal partial class PrefsStorageJsonCtx : JsonSerializerContext { }
