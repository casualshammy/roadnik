using Ax.Fw.Storage.Interfaces;

namespace Roadnik.Server.Interfaces;

internal interface IDbProvider
{
  IDocumentStorage GenericData { get; }
  IDocumentStorage Paths { get; }
}
