namespace Roadnik.Server.Modules.FCMProvider.Parts;

internal record FirebaseTokenResponse(
  string access_token,
  string token_type,
  int expires_in);
