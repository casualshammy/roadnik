using System.Windows.Input;

namespace Roadnik.MAUI.Data;

internal record BookmarkEntry(
  string RoomId,
  string Username);

internal record BookmarkEntryWrapper(
  BookmarkEntry Bookmark,
  string LocalizedUsername,
  Color DotColor,
  bool IsActive,
  ICommand OnDeleteCommand)
{
  private static readonly Color[] s_dotPalette =
  [
    Color.FromArgb("#6495ED"), // CornflowerBlue
    Color.FromArgb("#28C2D1"), // Cyan
    Color.FromArgb("#F7B548"), // Amber
    Color.FromArgb("#3E8EED"), // Blue
    Color.FromArgb("#5856D6"), // Indigo
    Color.FromArgb("#34C759"), // Green
    Color.FromArgb("#FF6B6B"), // Soft red
    Color.FromArgb("#A78BFA"), // Soft purple
    Color.FromArgb("#FF9F43"), // Orange
    Color.FromArgb("#26DE81"), // Mint
    Color.FromArgb("#FD79A8"), // Pink
    Color.FromArgb("#00B894"), // Teal
    Color.FromArgb("#FDCB6E"), // Yellow
    Color.FromArgb("#E17055"), // Coral
    Color.FromArgb("#74B9FF"), // Sky blue
    Color.FromArgb("#A29BFE"), // Lavender
  ];

  public static BookmarkEntryWrapper From(BookmarkEntry _bookmark, ICommand _onDeleteCommand, bool _isActive = false)
  {
    var hashCode = StableHash(_bookmark.RoomId);
    var dotColor = s_dotPalette[hashCode % (uint)s_dotPalette.Length];
    return new BookmarkEntryWrapper(
      _bookmark,
      $"👤 {_bookmark.Username}",
      dotColor,
      _isActive,
      _onDeleteCommand);
  }

  private static uint StableHash(string _s)
  {
    unchecked
    {
      var hash = 2166136261u;
      foreach (var c in _s)
      {
        hash ^= c;
        hash *= 16777619u;
      }
      return hash;
    }
  }
}
