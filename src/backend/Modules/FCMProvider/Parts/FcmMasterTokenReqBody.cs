namespace Roadnik.Modules.FCMProvider;

internal record FcmMasterTokenReqBody(string iss, string aud, string scope, long iat, long exp);