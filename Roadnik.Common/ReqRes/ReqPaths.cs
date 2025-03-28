namespace Roadnik.Common.ReqRes;

public static class ReqPaths
{
  [Obsolete] public static string GET_ROOM_PATHS { get; } = "/get";
  [Obsolete] public static string CREATE_NEW_POINT { get; } = "/create-new-point";
  public static string LIST_ROOM_POINTS { get; } = "/list-room-points";
  public static string DELETE_ROOM_POINT { get; } = "/delete-room-point";
  public static string GET_FREE_ROOM_ID { get; } = "/get-free-room-id";
  public static string IS_ROOM_ID_VALID { get; } = "/is-room-id-valid";
  public static string STORE_PATH_POINT { get; } = "/store-path-point";
  public static string REGISTER_ROOM { get; } = "/register-room";
  public static string UNREGISTER_ROOM { get; } = "/unregister-room";
  public static string LIST_REGISTERED_ROOMS { get; } = "/list-registered-rooms";
  public static string GET_VERSION { get; } = "/get-version";
  public static string LIST_ROOM_PATH_POINTS { get; } = "/list-room-path-points";
  public static string CREATE_ROOM_POINT { get; } = "/create-room-point";

}
