namespace Roadnik.Server.Interfaces;

internal interface IStravaTilesProvider
{
  IReadOnlyDictionary<string, string> Headers { get; }
}
