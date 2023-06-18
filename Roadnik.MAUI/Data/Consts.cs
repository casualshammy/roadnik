namespace Roadnik.MAUI.Data;

internal static class Consts
{
  public const string PREF_INITIALIZED = "settings.initialized";
  public const string PREF_DB_VERSION = "settings.db-version";
  public const string PREF_MAP_VIEW_STATE = "page.main.map-view-state";
  public const string PREF_MAP_LAYER = "page.main.map-layer";
  public const string PREF_MAP_SELECTED_TRACK = "page.main.map-selected-track";

  public const string PREF_SERVER_ADDRESS = "settings.network.server-address";
  public const string PREF_ROOM = "settings.network.room";
  public const string PREF_USERNAME = "settings.network.username";
  public const string PREF_TIME_INTERVAL = "settings.report.time-interval";
  public const string PREF_DISTANCE_INTERVAL = "settings.report.distance-interval";
  public const string PREF_TRACKPOINT_REPORTING_CONDITION = "settings.report.trackpoint-reporting-condition";
  public const string PREF_USER_MSG = "settings.report.user-msg";
  public const string PREF_MIN_ACCURACY = "settings.report.min-accuracy";
  public const string PREF_MAP_OPEN_BEHAVIOR = "settings.appearance.map-open-behavior";
  public const string PREF_MAP_CACHE_ENABLED = "settings.appearance.map-cache-enabled";
  public const string PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED = "settings.appearance.wipe-old-track-on-new-enabled";
  public const string PREF_NOTIFY_NEW_TRACK = "settings.notifications.on-new-track";
  public const string PREF_NOTIFY_NEW_POINT = "settings.notifications.on-new-point";

  public const string PREF_BOOKMARKS_LIST = "bookmarks.list";

  public const string JS_TO_CSHARP_MSG_TYPE_APP_LOADED = "js-msg-app-loaded";
  public const string HOST_MSG_REQUEST_DONE = "host-msg-request-done";
  public const string JS_TO_CSHARP_MSG_TYPE_INITIAL_DATA_RECEIVED = "js-msg-initial-data-received";
  public const string JS_TO_CSHARP_MSG_TYPE_MAP_LOCATION_CHANGED = "js-msg-map-location-changed";
  public const string JS_TO_CSHARP_MSG_TYPE_MAP_LAYER_CHANGED = "js-msg-map-layer-changed";
  public const string JS_TO_CSHARP_MSG_TYPE_NEW_TRACK = "js-msg-new-track";
  public const string JS_TO_CSHARP_MSG_TYPE_POPUP_OPENED = "js-msg-popup-opened";
  public const string JS_TO_CSHARP_MSG_TYPE_POPUP_CLOSED = "js-msg-popup-closed";
  public const string JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED = "js-msg-waypoint-add-started";

}
