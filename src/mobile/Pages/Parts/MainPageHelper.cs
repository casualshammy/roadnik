using Ax.Fw.Extensions;
using Roadnik.MAUI.Data.JsonBridge;
using Roadnik.MAUI.JsonCtx;
using System.Globalization;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Web;
using static Roadnik.MAUI.Data.AppConsts;
using static Roadnik.MAUI.Data.PageConsts.MainPageConsts;

namespace Roadnik.MAUI.Pages.Parts;

internal static class MainPageHelper
{
  public static string GetWebAppAddress(
    string _serverAddress,
    string? _roomId,
    HostMsgMapStateData? _mapState = null)
  {
    var serverUri = new Uri(_serverAddress);
    var urlBuilder = new UriBuilder($"{serverUri.Scheme}://{WEBAPP_HOST}:{serverUri.Port}/r/");

    var query = HttpUtility.ParseQueryString(urlBuilder.Query);
    query["id"] = _roomId;
    query["api_url"] = _serverAddress;
    if (_mapState?.Lat != null)
      query["lat"] = _mapState.Lat.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.Lng != null)
      query["lng"] = _mapState.Lng.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.Zoom != null)
      query["zoom"] = ((int)_mapState.Zoom).ToString(CultureInfo.InvariantCulture);
    if (_mapState != null && !_mapState.Layer.IsNullOrWhiteSpace())
      query["layer"] = _mapState.Layer;
    if (_mapState?.Overlays != null)
    {
      var json = JsonSerializer.Serialize(_mapState.Overlays, JsBridgeJsonCtx.Default.IReadOnlyListString);
      var jsonBytes = Encoding.UTF8.GetBytes(json);
      var base64 = Convert.ToBase64String(jsonBytes);
      query["overlays"] = base64;
    }
    if (_mapState != null && !_mapState.SelectedAppId.IsNullOrWhiteSpace())
      query["selected_app_id"] = _mapState.SelectedAppId;
    if (_mapState?.SelectedPathWindowLeft != null)
      query["selected_path_window_left"] = _mapState.SelectedPathWindowLeft.Value.ToString(CultureInfo.InvariantCulture); ;
    if (_mapState?.SelectedPathWindowBottom != null)
      query["selected_path_window_bottom"] = _mapState.SelectedPathWindowBottom.Value.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.UsbLeft != null)
      query["usb_left"] = _mapState.UsbLeft.Value.ToString(CultureInfo.InvariantCulture);
    if (_mapState?.UsbBottom != null)
      query["usb_bottom"] = _mapState.UsbBottom.Value.ToString(CultureInfo.InvariantCulture);

    urlBuilder.Query = query.ToString();

    var url = urlBuilder.ToString();
    return url;
  }

  public static double CalcSideBtnTargetWidth(string _measureText)
  {
    var density = DeviceDisplay.Current.MainDisplayInfo.Density;
    var screenWidth = DeviceDisplay.Current.MainDisplayInfo.Width / density;
    var maxTargetWidth = screenWidth * 0.8;

    var context = global::Android.App.Application.Context;
    using var paint = new Android.Graphics.Paint();
    paint.TextSize = Android.Util.TypedValue.ApplyDimension(
      Android.Util.ComplexUnitType.Sp,
      (float)MAP_SIDE_BTN_LABEL_FONT_SIZE_SP,
      context.Resources!.DisplayMetrics);

    var textWidthDp = paint.MeasureText(_measureText) / density;
    var targetWidth = Math.Min(maxTargetWidth, MAP_SIDE_BTN_COLLAPSED_SIZE + textWidthDp + 16);
    return Math.Max(MAP_SIDE_BTN_COLLAPSED_SIZE, targetWidth);
  }

  public static async Task ExpandSideBtnAsync(
    VisualElement _frame,
    string _animName,
    Func<Rect> _getBounds,
    Action<Rect> _setBounds,
    Func<double> _getOpacity,
    Action<double> _setOpacity,
    double _targetWidth)
  {
    var bounds = _getBounds();
    if (bounds.Width >= _targetWidth)
      return;

    var startWidth = bounds.Width;
    var startOpacity = _getOpacity();
    var tcs = new TaskCompletionSource<Unit>();

    var animation = new Animation();
    animation.Add(0, 1, new Animation(_t =>
    {
      var w = startWidth + (_targetWidth - startWidth) * _t;
      _setBounds(new Rect(bounds.X, bounds.Y, w, bounds.Height));
    }));
    animation.Add(0, 1, new Animation(_t =>
    {
      const double expandedOpacity = MAP_SIDE_BTN_COLLAPSED_OPACITY * 2;
      _setOpacity(startOpacity + (expandedOpacity - startOpacity) * _t);
    }));

    animation.Commit(_frame, _animName, 16, 300, Easing.CubicOut, (_, __) => tcs.TrySetResult(Unit.Default));
    await tcs.Task;
  }

  public static async Task CollapseSideBtnAsync(
    VisualElement _frame,
    string _animName,
    Func<Rect> _getBounds,
    Action<Rect> _setBounds,
    Func<double> _getOpacity,
    Action<double> _setOpacity,
    Action _clearStatus)
  {
    var bounds = _getBounds();
    if (bounds.Width <= MAP_SIDE_BTN_COLLAPSED_SIZE)
    {
      _setOpacity(MAP_SIDE_BTN_COLLAPSED_OPACITY);
      _clearStatus();
      return;
    }

    var startWidth = bounds.Width;
    var startOpacity = _getOpacity();
    var tcs = new TaskCompletionSource<Unit>();

    var animation = new Animation();
    animation.Add(0, 1, new Animation(_t =>
    {
      var w = startWidth + (MAP_SIDE_BTN_COLLAPSED_SIZE - startWidth) * _t;
      _setBounds(new Rect(bounds.X, bounds.Y, w, bounds.Height));
    }));
    animation.Add(0, 1, new Animation(_t =>
    {
      _setOpacity(startOpacity + (MAP_SIDE_BTN_COLLAPSED_OPACITY - startOpacity) * _t);
    }));

    animation.Commit(_frame, _animName, 16, 300, Easing.CubicIn, (_, __) => tcs.TrySetResult(Unit.Default));
    await tcs.Task;

    _clearStatus();
  }
}
