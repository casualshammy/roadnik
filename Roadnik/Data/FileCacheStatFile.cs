namespace Roadnik.Server.Data;

internal readonly record struct FileCacheStatFile(
  long TotalFolders,
  long TotalFiles,
  long TotalSizeBytes,
  double StatFileGenerationTimeMs,
  DateTimeOffset Generated);
