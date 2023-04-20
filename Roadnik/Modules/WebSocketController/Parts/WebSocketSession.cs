using System.Net.WebSockets;

namespace Roadnik.Modules.WebSocketController.Parts;

public record WebSocketSession(string Key, WebSocket Socket);
