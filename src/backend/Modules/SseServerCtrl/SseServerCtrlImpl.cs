using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Ax.Fw.Web.Data.SseServer;
using Ax.Fw.Web.Modules.SseServer;
using Roadnik.Server.Interfaces;
using Roadnik.Server.JsonCtx;
using System.Reactive.Linq;

namespace Roadnik.Server.Modules.SseServerCtrl;

internal sealed class SseServerCtrlImpl
   : ISseServerCtrl, IAppModule<ISseServerCtrl>
{
  public static ISseServerCtrl ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILog _log,
      IReadOnlyLifetime _lifetime)
      => new SseServerCtrlImpl(_log["sse"], _lifetime));
  }

  private readonly SseServerImpl<Guid, string> p_sseServer;

  private SseServerCtrlImpl(
    ILog _log,
    IReadOnlyLifetime _lifetime)
  {
    p_sseServer = new SseServerImpl<Guid, string>(
      _lifetime,
      _log,
      SseJsonCtx.Default,
      TimeSpan.FromSeconds(30),
      100);

    Observable
      .Merge(
        p_sseServer.ClientConnected.Select(_ => 1),
        p_sseServer.ClientDisconnected.Select(_ => -1))
      .Scan(0, (_acc, _delta) => _acc + _delta)
      .Subscribe(_ => _log.Info($"**Clients connected**: __{_}__"), _lifetime);
  }

  public IObservable<SseSession<Guid, string>> ClientConnected => p_sseServer.ClientConnected;

  public IDisposable AcceptClient(
    string _roomId,
    out SseSession<Guid, string> _session)
    => p_sseServer.AcceptClient(Guid.NewGuid(), _roomId, out _session);

  public void SendMsg<T>(SseSession<Guid, string> _session, T _msg)
    where T : notnull, SseAbstractMsg
    => p_sseServer.SendMsg(_session, _msg);

  public void SendMsgByRoomId<T>(string _roomId, T _msg)
    where T : notnull, SseAbstractMsg
    => p_sseServer.PostBroadcastMsg(_roomId, _msg);

}
