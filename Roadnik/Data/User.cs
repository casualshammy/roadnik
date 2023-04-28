using Ax.Fw.SharedTypes.Attributes;

namespace Roadnik.Data;

[SimpleDocument("user")]
public record User(string Key, string Email, DateTimeOffset? ValidUntil);
