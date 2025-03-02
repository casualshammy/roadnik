using Ax.Fw.Extensions;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Collections.Concurrent;
using System.Windows.Input;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Pages;

public partial class BookmarksPage : CContentPage
{
  private readonly IPreferencesStorage p_preferences;
  private readonly ConcurrentDictionary<int, BookmarkEntry> p_bookmarks = new();

  public BookmarksPage()
  {
    InitializeComponent();
    BindingContext = this;

    Title = MAUI.Resources.Strings.AppResources.shell_bookmarks;

    p_preferences = Container.Locate<IPreferencesStorage>();
    var bookmarks = p_preferences.GetValueOrDefault<List<BookmarkEntry>>(PREF_BOOKMARKS_LIST) ?? new List<BookmarkEntry>();
    foreach (var bookmark in bookmarks)
    {
      var hashCode = HashCode.Combine(bookmark.ServerAddress, bookmark.RoomId, bookmark.Username);
      p_bookmarks.TryAdd(hashCode, bookmark);
    }

    p_listView.ItemsSource = bookmarks;

    OnDeleteCommand = new Command(_o =>
    {
      if (_o is not BookmarkEntry bookmark)
        return;

      var hashCode = HashCode.Combine(bookmark.ServerAddress, bookmark.RoomId, bookmark.Username);
      if (p_bookmarks.TryRemove(hashCode, out _))
      {
        var list = p_bookmarks.Values.ToList();
        p_preferences.SetValue(PREF_BOOKMARKS_LIST, list);
        p_listView.ItemsSource = list;
      }
    });

  }

  public ICommand OnDeleteCommand { get; }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    p_pullRightLabel.Opacity = 1d;
    p_pullRightLabel.IsVisible = true;

    var animation = new Animation(_d => p_pullRightLabel.Opacity = _d, 1.0d, 0.0d, Easing.BounceIn, () => p_pullRightLabel.IsVisible = false);
    animation.Commit(p_pullRightLabel, "pullRightOpacity", 16, 5000);
  }

  private async void ListView_ItemTapped(object _sender, ItemTappedEventArgs _e)
  {
    if (_e.Item is not BookmarkEntry bookmark)
      return;

    var dialogResult = await DisplayAlert(
      "Do you want to use the following credentials?",
      $"Server: {bookmark.ServerAddress}\nRoom: {bookmark.RoomId}\nUsername: {bookmark.Username}",
      "Yes",
      "No");

    if (!dialogResult)
      return;

    p_preferences.SetValue(PREF_ROOM, bookmark.RoomId);
    p_preferences.SetValue(PREF_USERNAME, bookmark.Username);

    await Toast.Make("Done").Show();
  }

  private async void AddCurrentCredentials_Clicked(object _sender, EventArgs _e)
  {
    var server = DEBUG_APP_ADDRESS ?? ROADNIK_APP_ADDRESS;
    if (server.IsNullOrWhiteSpace())
    {
      await DisplayAlert("Current server address is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var roomId = p_preferences.GetValueOrDefault<string>(PREF_ROOM);
    if (roomId.IsNullOrWhiteSpace())
    {
      await DisplayAlert("Current room id is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var username = p_preferences.GetValueOrDefault<string>(PREF_USERNAME);
    if (username.IsNullOrWhiteSpace())
    {
      await DisplayAlert("Current username is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var hashCode = HashCode.Combine(server, roomId, username);
    if (!p_bookmarks.TryAdd(hashCode, new BookmarkEntry(server, roomId, username)))
    {
      await DisplayAlert("These credentials are already added to bookmarks", null, "Close");
      return;
    }

    var list = p_bookmarks.Values.ToList();
    p_preferences.SetValue(PREF_BOOKMARKS_LIST, list);
    p_listView.ItemsSource = list;
  }

}