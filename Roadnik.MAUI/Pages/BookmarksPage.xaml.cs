using Ax.Fw.Extensions;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using static Roadnik.MAUI.Data.Consts;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages;

public partial class BookmarksPage : CContentPage
{
  private readonly IPreferencesStorage p_preferences;
  private readonly ConcurrentDictionary<int, BookmarkEntryWrapper> p_bookmarks = new();
  private readonly ObservableCollection<BookmarkEntryWrapper> p_bookmarksObservable = new();
  private readonly Command<BookmarkEntryWrapper> p_onDeleteCommand;

  public BookmarksPage()
  {
    InitializeComponent();
    BindingContext = this;
    Title = L.shell_bookmarks;

    p_preferences = Container.Locate<IPreferencesStorage>();
    p_onDeleteCommand = new Command<BookmarkEntryWrapper>(_o =>
    {
      var bookmark = _o.Bookmark;
      var hashCode = HashCode.Combine(bookmark.RoomId, bookmark.Username);
      if (p_bookmarks.TryRemove(hashCode, out var removed))
      {
        p_preferences.SetValue(PREF_BOOKMARKS_LIST, p_bookmarks.Values.Select(_ => _.Bookmark).ToArray());
        p_bookmarksObservable.Remove(removed);
      }
    });

    var bookmarks = p_preferences.GetValueOrDefault<List<BookmarkEntry>>(PREF_BOOKMARKS_LIST) ?? [];
    foreach (var bookmark in bookmarks)
    {
      var hashCode = HashCode.Combine(bookmark.RoomId, bookmark.Username);
      var wrapper = BookmarkEntryWrapper.From(bookmark, p_onDeleteCommand);
      if (p_bookmarks.TryAdd(hashCode, wrapper))
        p_bookmarksObservable.Add(wrapper);
    }

    p_listView.ItemsSource = p_bookmarksObservable;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    p_pullRightLabel.Opacity = 1d;
    p_pullRightLabel.IsVisible = true;

    var animation = new Animation(_d => p_pullRightLabel.Opacity = _d, 1.0d, 0.0d, Easing.BounceIn, () => p_pullRightLabel.IsVisible = false);
    animation.Commit(p_pullRightLabel, "pullRightOpacity", 16, 5000);
  }

  private async void CollectionView_SelectionChanged(object _sender, SelectionChangedEventArgs _e)
  {
    if (_e.CurrentSelection.FirstOrDefault() is not BookmarkEntryWrapper wrapper)
      return;

    ((CollectionView)_sender).SelectedItem = null;

    var dialogResult = await DisplayAlertAsync(
      "Do you want to use the following credentials?",
      $"Room: {wrapper.Bookmark.RoomId}\nUsername: {wrapper.Bookmark.Username}",
      "Yes",
      "No");

    if (!dialogResult)
      return;

    p_preferences.SetValue(PREF_ROOM, wrapper.Bookmark.RoomId);
    p_preferences.SetValue(PREF_USERNAME, wrapper.Bookmark.Username);

    await Toast.Make("Done").Show();
  }

  private async void AddCurrentCredentials_Clicked(object _sender, EventArgs _e)
  {
    var roomId = p_preferences.GetValueOrDefault<string>(PREF_ROOM);
    if (roomId.IsNullOrWhiteSpace())
    {
      await DisplayAlertAsync("Current room id is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var username = p_preferences.GetValueOrDefault<string>(PREF_USERNAME);
    if (username.IsNullOrWhiteSpace())
    {
      await DisplayAlertAsync("Current username is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var hashCode = HashCode.Combine(roomId, username);
    var bookmark = new BookmarkEntry(roomId, username);
    var wrapper = BookmarkEntryWrapper.From(bookmark, p_onDeleteCommand);
    if (!p_bookmarks.TryAdd(hashCode, wrapper))
    {
      await DisplayAlertAsync("These credentials are already added to bookmarks", null, "Close");
      return;
    }

    p_preferences.SetValue(PREF_BOOKMARKS_LIST, p_bookmarks.Values.Select(_ => _.Bookmark).ToArray());
    p_bookmarksObservable.Add(wrapper);
  }

}