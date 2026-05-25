using System.Reactive;
using static Roadnik.MAUI.Data.PageConsts.MainPageConsts;

namespace Roadnik.MAUI.Pages.Parts;

internal static class MainPageSideBtnHelper
{
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
