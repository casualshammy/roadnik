using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Toolkit;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using static Roadnik.MAUI.Data.AppConsts;
using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages;

public partial class BookmarksPage : CContentPage
{
  private readonly IPreferencesStorage p_preferences;
  private readonly IReadOnlyLifetime p_lifetime;
  private readonly ConcurrentDictionary<int, BookmarkEntryWrapper> p_bookmarks = new();
  private readonly ObservableCollection<BookmarkEntryWrapper> p_bookmarksObservable = new();
  private readonly Command<BookmarkEntryWrapper> p_onDeleteCommand;
  private string p_searchText = string.Empty;

  public BookmarksPage()
  {
    InitializeComponent();
    Title = L.shell_bookmarks;

    p_preferences = Container.Locate<IPreferencesStorage>();
    p_lifetime = Container.Locate<IReadOnlyLifetime>();

    p_onDeleteCommand = new Command<BookmarkEntryWrapper>(_o =>
    {
      var bookmark = _o.Bookmark;
      var hashCode = HashCode.Combine(bookmark.RoomId, bookmark.Username);
      if (p_bookmarks.TryRemove(hashCode, out _))
        p_preferences.SetValue(PREF_BOOKMARKS_LIST, [.. p_bookmarks.Values.Select(_ => _.Bookmark)], PrefsStorageJsonCtx.Default.IReadOnlyListBookmarkEntry);
    });

    p_preferences.PreferencesChanged
      .DistinctUntilChanged(_ =>
      {
        var bookmarks = p_preferences.GetValueOrDefault(PREF_BOOKMARKS_LIST, PrefsStorageJsonCtx.Default.IReadOnlyListBookmarkEntry) ?? [];
        var hash = bookmarks.Aggregate(0, (_acc, _entry) => _acc ^ _entry.GetHashCode());
        return hash;
      })
      .Subscribe(_ =>
      {
        p_bookmarks.Clear();

        var bookmarks = p_preferences.GetValueOrDefault(PREF_BOOKMARKS_LIST, PrefsStorageJsonCtx.Default.IReadOnlyListBookmarkEntry) ?? [];
        foreach (var bookmark in bookmarks)
        {
          var hashCode = HashCode.Combine(bookmark.RoomId, bookmark.Username);
          var wrapper = BookmarkEntryWrapper.From(bookmark, p_onDeleteCommand);
          p_bookmarks.TryAdd(hashCode, wrapper);
        }

        MainThread.BeginInvokeOnMainThread(RecalculateData);
      }, p_lifetime);

    p_preferences.PreferencesChanged
      .DistinctUntilChanged(_ =>
      {
        var activeRoom = p_preferences.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
        var activeUser = p_preferences.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String);
        return HashCode.Combine(activeRoom, activeUser);
      })
      .Subscribe(_ => MainThread.BeginInvokeOnMainThread(RecalculateData), p_lifetime);

    p_listView.ItemsSource = p_bookmarksObservable;

    BindingContext = this;
  }

  public string SearchText
  {
    get => p_searchText;
    set
    {
      p_searchText = value;
      OnPropertyChanged();
      RecalculateData();
    }
  }

  public bool HasBookmarks => p_bookmarksObservable.Count > 0;
  public bool IsEmpty => p_bookmarksObservable.Count == 0;

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

    p_preferences.SetValue(PREF_ROOM, wrapper.Bookmark.RoomId, PrefsStorageJsonCtx.Default.String);
    p_preferences.SetValue(PREF_USERNAME, wrapper.Bookmark.Username, PrefsStorageJsonCtx.Default.String);

    await Toast.Make("Done").Show();
  }

  private async void AddCurrentCredentials_Clicked(object _sender, EventArgs _e)
  {
    var roomId = p_preferences.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
    if (roomId.IsNullOrWhiteSpace())
    {
      await DisplayAlertAsync("Current room id is empty", "Please go to options page and fill it", "Close");
      return;
    }

    var username = p_preferences.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String);
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

    p_preferences.SetValue(PREF_BOOKMARKS_LIST, [.. p_bookmarks.Values.Select(_ => _.Bookmark)], PrefsStorageJsonCtx.Default.IReadOnlyListBookmarkEntry);
  }

  private void RecalculateData()
  {
    var activeRoom = p_preferences.GetValueOrDefault(PREF_ROOM, PrefsStorageJsonCtx.Default.String);
    var activeUser = p_preferences.GetValueOrDefault(PREF_USERNAME, PrefsStorageJsonCtx.Default.String);

    foreach (var (hashCode, existing) in p_bookmarks)
    {
      var shouldBeActive = existing.Bookmark.RoomId == activeRoom && existing.Bookmark.Username == activeUser;
      if (existing.IsActive != shouldBeActive)
        p_bookmarks[hashCode] = existing with { IsActive = shouldBeActive };
    }

    var filter = p_searchText.Trim();
    var sorted = p_bookmarks.Values
      .OrderBy(_ => _.Bookmark.RoomId)
      .ThenBy(_ => _.Bookmark.Username);

    IEnumerable<BookmarkEntryWrapper> filtered = sorted;
    if (!string.IsNullOrEmpty(filter))
      filtered = sorted.Where(_ =>
        _.Bookmark.RoomId.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        _.Bookmark.Username.Contains(filter, StringComparison.OrdinalIgnoreCase));

    p_bookmarksObservable.Clear();
    foreach (var item in filtered)
      p_bookmarksObservable.Add(item);

    OnPropertyChanged(nameof(HasBookmarks));
    OnPropertyChanged(nameof(IsEmpty));
  }

}