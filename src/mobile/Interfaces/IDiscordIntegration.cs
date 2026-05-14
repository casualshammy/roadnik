namespace Roadnik.MAUI.Interfaces;

internal interface IDiscordIntegration
{
  /// <summary>
  /// Clears stored tokens and disconnects from Discord Gateway.
  /// </summary>
  void RevokeAuth();

  /// <summary>
  /// Pushes a new location to Discord presence. Fire-and-forget safe.
  /// </summary>
  void UpdatePresence(double _lat, double _lng, string _roomId, float? _speed, int? _hrm);

  /// <summary>
  /// Clears the Discord presence status. Fire-and-forget safe.
  /// </summary>
  void ClearPresence();

  /// <summary>
  /// Asynchronously retrieves the username associated with the specified authentication token.
  /// </summary>
  /// <param name="_token">The authentication token used to identify the user. Cannot be null or empty.</param>
  /// <param name="_ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the username if the token is valid;
  /// otherwise, null.</returns>
  Task<string?> FetchUsernameAsync(string _token, CancellationToken _ct);
}
