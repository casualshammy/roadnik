using System.Windows.Input;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Data;

internal record BookmarkEntry(string ServerAddress, string RoomId, string Username);

internal record BookmarkEntryWrapper(
  BookmarkEntry Bookmark, 
  string LocalizedRoomId,
  string LocalizedUsername,
  ICommand OnDeleteCommand)
{
  public static BookmarkEntryWrapper From(BookmarkEntry _bookmark, ICommand _onDeleteCommand)
  {
    return new BookmarkEntryWrapper(
      _bookmark,
      $"{L.page_bookmarks_room}: {_bookmark.RoomId}",
      $"{L.page_bookmarks_username}: {_bookmark.Username}",
      _onDeleteCommand);
  }
}
