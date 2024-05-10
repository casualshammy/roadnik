namespace Roadnik.Common.ReqRes;

public record IsUdpTransportAvailableRes(
  string Endpoint, 
  string PublicKeyHash);