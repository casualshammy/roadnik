﻿using Roadnik.Modules.WebSocketController.Parts;
using System.Net.WebSockets;

namespace Roadnik.Interfaces;

public interface IWebSocketCtrl
{
  IObservable<object> IncomingMessages { get; }
  IObservable<WebSocketSession> ClientConnected { get; }

  Task<bool> AcceptSocket(string _key, WebSocket _webSocket);
  Task<int> BroadcastMsgAsync<T>(T _msg, CancellationToken _ct) where T : notnull;
  Task SendMsgAsync<T>(WebSocketSession _session, T _msg, CancellationToken _ct) where T : notnull;
  Task SendMsgByKeyAsync<T>(string _key, T _msg, CancellationToken _ct) where T : notnull;
}