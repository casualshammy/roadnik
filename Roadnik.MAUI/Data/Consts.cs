﻿using Plugin.BLE.Abstractions.Extensions;

namespace Roadnik.MAUI.Data;

internal static class Consts
{
  public static string? DEBUG_APP_ADDRESS = null; // "http://192.168.1.100:5544"; // null;

  public const string ROADNIK_APP_ADDRESS = "https://roadnik.app";
  public const string WEBAPP_HOST = "webapp.local";
  public const int PRIVACY_POLICY_VERSION = 4;

  public const string PREF_DB_VERSION = "settings.db-version";
  public const string PREF_PRIVACY_POLICY_VERSION = "settings.privacy-policy-version";

  //public const string PREF_SERVER_ADDRESS = "settings.network.server-address";
  public const string PREF_ROOM = "settings.network.room";
  public const string PREF_USERNAME = "settings.network.username";
  public const string PREF_TIME_INTERVAL = "settings.report.time-interval";
  public const string PREF_DISTANCE_INTERVAL = "settings.report.distance-interval";
  public const string PREF_TRACKPOINT_REPORTING_CONDITION = "settings.report.trackpoint-reporting-condition.v2";
  public const string PREF_MIN_ACCURACY = "settings.report.min-accuracy";
  public const string PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED = "settings.appearance.wipe-old-track-on-new-enabled";
  public const string PREF_NOTIFY_NEW_TRACK = "settings.notifications.on-new-track";
  public const string PREF_NOTIFY_NEW_POINT = "settings.notifications.on-new-point";
  public const string PREF_WEBAPP_MAP_STATE = "webapp.map-state";
  //public const string PREF_LOCATION_PROVIDER = "settings.report.location-provider";
  public const string PREF_LOCATION_PROVIDERS = "settings.report.location-providers";
  public const string PREF_BLE_HRM_ENABLED = "settings.report.ble-hrm-enabled";
  public const string PREF_BLE_HRM_DEVICE_INFO = "settings.report.ble-hrm-device-info";
  public const string PREF_APP_INSTALLATION_ID = "app.installation-id";

  public const string PREF_BOOKMARKS_LIST = "bookmarks.list";

  public const string HOST_MSG_TRACKS_SYNCHRONIZED = "host-msg-tracks-synchronized";
  public const string JS_TO_CSHARP_MSG_TYPE_WAYPOINT_ADD_STARTED = "js-msg-waypoint-add-started";
  public const string HOST_MSG_MAP_STATE = "host-msg-map-state";
  public const string HOST_MSG_MAP_DRAG_STARTED = "map-drag-started";

  public const string NOTIFICATION_CHANNEL_MAP_EVENTS = "MapEventsChannel";
  public const int NOTIFICATION_ID_RECORDING = 100;
  public const int NOTIFICATION_ID_LOCATION_PROVIDER_DISABLED = 101;

  public const int PUSH_MSG_NEW_POINT = 10000;
  public const int PUSH_MSG_NEW_TRACK = 10001;

  public const string DEEP_LINK_INTENT_KEY = "open-link";
  public const string INTENT_STOP_LOC_SHARING = "stop-sharing";

  public static Guid BLE_SERVICE_ID_HEART_RATE { get; } = GuidExtension.UuidFromPartial(0x180d);
  public static Guid BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT { get; } = GuidExtension.UuidFromPartial(0x2A37);

}
