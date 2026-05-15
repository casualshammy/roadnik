namespace Roadnik.MAUI.Interfaces;

internal interface IDiscordIntegration
{
  /// <summary>
  /// Clears stored tokens and disconnects from Discord Gateway.
  /// </summary>
  void RevokeAuth();

  /// <summary>
  /// Pushes a new location to Discord presence.
  /// </summary>
  void UpdatePresence(
    int _sessionId,
    double _lat,
    double _lng,
    string _roomId,
    float? _speed,
    int? _hrm);

  /// <summary>
  /// Clears the Discord presence status.
  /// </summary>
  void ClearPresence();

}
