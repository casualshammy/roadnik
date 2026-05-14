namespace Roadnik.MAUI.Interfaces;

public interface IDeepLinksController
{
  Task NewDeepLinkAsync(string _url, CancellationToken _ct = default);
}

