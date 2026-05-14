using Ax.Fw.Storage.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace Roadnik.MAUI.Interfaces;

public interface IWebDataCache
{
  IBlobStorage Cache { get; }

  void EnqueueDownload(
    string _url);

  bool TryGetStream(
    string _url,
    [NotNullWhen(true)] out Stream? _stream,
    [NotNullWhen(true)] out string? _mime);
}