using Ax.Fw.Cache;
using System.Diagnostics.CodeAnalysis;

namespace Roadnik.MAUI.Interfaces;

public interface IWebDataCache
{
  FileCache Cache { get; }

  void EnqueueDownload(string _url);
  bool TryGetStream(
    string _url,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _mime);
}