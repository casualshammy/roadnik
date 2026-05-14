namespace Roadnik.Server.Modules.FCMProvider.Parts;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

internal class FcmMsg
{
  public FcmMsgContent Message { get; set; }
}

internal class FcmMsgContent
{
  public string Topic { get; set; }
  public FcmMsgContentData Data { get; set; }
  public FcmMsgContentAndroid Android { get; set; }
}

internal class FcmMsgContentData
{
  public string JsonData { get; set; }
}

internal class FcmMsgContentAndroid
{
  public string Ttl { get; set; }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
