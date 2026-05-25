namespace Roadnik.MAUI.Data.PageConsts;

internal static class MainPageConsts
{
  public const double MAP_SIDE_BTN_COLLAPSED_SIZE = 44.0;
  public const double MAP_SIDE_BTN_COLLAPSED_OPACITY = 0.35;
  public const double MAP_SIDE_BTN_INITIAL_X = 10.0;
  public const double MAP_SIDE_BTN_DISCORD_INITIAL_Y = 120.0;
  public const double MAP_SIDE_BTN_HRM_INITIAL_Y = MAP_SIDE_BTN_DISCORD_INITIAL_Y + MAP_SIDE_BTN_COLLAPSED_SIZE + 6.0;
  public static readonly CornerRadius MAP_SIDE_BTN_CORNER_RADIUS = new(MAP_SIDE_BTN_COLLAPSED_SIZE / 2);
  public static readonly Thickness MAP_SIDE_BTN_LABEL_MARGIN = new(MAP_SIDE_BTN_COLLAPSED_SIZE, 0, 8, 0);
  public const double MAP_SIDE_BTN_LABEL_FONT_SIZE_SP = 13.0;
  public static Brush DISCORD_BTN_BRUSH = new SolidColorBrush(Color.FromArgb("#5865F2"));
  public static Brush HRM_BTN_BRUSH = new SolidColorBrush(Color.FromArgb("#E53935"));
}
