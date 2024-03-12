using Roadnik.Server.Data;
using System.Text.Json.Serialization;

namespace Roadnik.Server.JsonCtx;

[JsonSourceGenerationOptions(
  WriteIndented = true)]
[JsonSerializable(typeof(FileCacheStatFile))]
internal partial class FileCacheJsonCtx : JsonSerializerContext
{

}
