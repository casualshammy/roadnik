using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("user")]
public record User(string RoomId, string Email, DateTimeOffset? ValidUntil);
