using Ax.Fw.Web.Data.SseServer;

namespace Roadnik.Server.Interfaces;

internal interface ISseServerCtrl
{
  IObservable<SseSession<Guid, string>> ClientConnected { get; }

  IDisposable AcceptClient(string _roomId, out SseSession<Guid, string> _session);
  void SendMsg<T>(SseSession<Guid, string> _session, T _msg) where T : notnull;
  void SendMsgByRoomId<T>(string _roomId, T _msg) where T : notnull;
}
