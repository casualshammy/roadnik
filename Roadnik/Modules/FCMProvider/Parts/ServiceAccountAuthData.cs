namespace Roadnik.Server.Modules.FCMProvider.Parts;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

internal class ServiceAccountAuthData
{
  public string type { get; set; }
  public string project_id { get; set; }
  public string private_key_id { get; set; }
  public string private_key { get; set; }
  public string client_email { get; set; }
  public string client_id { get; set; }
  public string auth_uri { get; set; }
  public string token_uri { get; set; }
  public string auth_provider_x509_cert_url { get; set; }
  public string client_x509_cert_url { get; set; }
  public string universe_domain { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.