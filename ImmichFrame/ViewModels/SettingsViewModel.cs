using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Models;
using ImmichFrame.Helpers;
using ImmichFrame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImmichFrame.ViewModels;

public partial class SettingsViewModel : NavigatableViewModelBase, IDisposable
{
    private const int PersonThumbnailDecodeWidth = 80;
    private const int UnnamedPeoplePerRow = 10;
    private readonly SemaphoreSlim peopleLoadGate = new(1, 1);
    private readonly SemaphoreSlim thumbnailLoadGate = new(2, 2);
    private IReadOnlyList<PersonInfo> allPeople = Array.Empty<PersonInfo>();
    private CancellationTokenSource thumbnailLoadCancellation = new();
    private ImmichFrameLogic? peopleLogic;
    private int disposed;

    [ObservableProperty]
    private ObservableCollection<PersonListItem> availablePeople = new();

    [ObservableProperty]
    private ObservableCollection<PersonListItem> includedPeople = new();

    [ObservableProperty]
    private ObservableCollection<PersonListItem> excludedPeople = new();

    [ObservableProperty]
    private ObservableCollection<PersonGridRow> unnamedPeopleRows = new();

    [ObservableProperty]
    private PersonListItem? selectedAvailablePerson;

    [ObservableProperty]
    private PersonListItem? selectedIncludedPerson;

    [ObservableProperty]
    private PersonListItem? selectedExcludedPerson;

    [ObservableProperty]
    private PersonListItem? selectedUnnamedPerson;

    [ObservableProperty]
    private string peopleStatus = "Loading people…";

    [ObservableProperty]
    private string unnamedSelectionStatus = "Tap an unnamed face to change its filter.";

    [ObservableProperty]
    private bool isPeopleLoading;

    [ObservableProperty]
    private ObservableCollection<ListItem> albumList;

    [ObservableProperty]
    private ObservableCollection<ListItem> excludedAlbumList;

    [ObservableProperty]
    private bool cancelVisible = true;

    [ObservableProperty]
    public Settings settings;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand QuitCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand RefreshPeopleCommand { get; }
    public ICommand IncludePersonCommand { get; }
    public ICommand ExcludePersonCommand { get; }
    public ICommand RemoveIncludedPersonCommand { get; }
    public ICommand RemoveExcludedPersonCommand { get; }
    public ICommand SelectUnnamedPersonCommand { get; }
    public ICommand MakeUnnamedAvailableCommand { get; }
    public ICommand IncludeUnnamedPersonCommand { get; }
    public ICommand ExcludeUnnamedPersonCommand { get; }
    public ICommand AddAlbumCommand { get; }
    public ICommand RemoveAlbumCommand { get; }
    public ICommand AddExcludedAlbumCommand { get; }
    public ICommand RemoveExcludedAlbumCommand { get; }
    public ICommand TestMarginCommand { get; }
    public List<string> StretchOptions { get; } = Enum.GetNames(typeof(Stretch)).ToList();
    public IReadOnlyList<string> LetterboxBackgroundChoices { get; } = LetterboxBackgroundOptions.All;
    public IReadOnlyList<string> TransitionAnimationChoices { get; } = TransitionAnimationOptions.All;
    public List<string> ImageLocationOptions { get; } = new() { "City", "City,State", "City,State,Country" };

    public SettingsViewModel() : this(true) { }

    public SettingsViewModel(bool cancelEnabled = true)
    {
        try
        {
            Settings = Settings.CurrentSettings;
        }
        catch (SettingsNotValidException)
        {
            Settings = new Settings();
        }

        SaveCommand = new RelayCommand(SaveAction);
        CancelCommand = new RelayCommand(CancelAction);
        QuitCommand = new RelayCommand(QuitAction);
        BackupCommand = new RelayCommand(BackupAction);
        RestoreCommand = new RelayCommand(RestoreAction);
        RefreshPeopleCommand = new RelayCommand(async () => await LoadPeopleAsync());
        IncludePersonCommand = new RelayCommand(IncludeSelectedPerson);
        ExcludePersonCommand = new RelayCommand(ExcludeSelectedPerson);
        RemoveIncludedPersonCommand = new RelayCommand(RemoveSelectedIncludedPerson);
        RemoveExcludedPersonCommand = new RelayCommand(RemoveSelectedExcludedPerson);
        SelectUnnamedPersonCommand = new RelayCommandParams(SelectUnnamedPerson);
        MakeUnnamedAvailableCommand = new RelayCommand(() => SetSelectedUnnamedState(PersonSelectionState.Available));
        IncludeUnnamedPersonCommand = new RelayCommand(() => SetSelectedUnnamedState(PersonSelectionState.Included));
        ExcludeUnnamedPersonCommand = new RelayCommand(() => SetSelectedUnnamedState(PersonSelectionState.Excluded));
        AddAlbumCommand = new RelayCommand(AddAlbumAction);
        RemoveAlbumCommand = new RelayCommandParams(RemoveAlbumAction);
        AddExcludedAlbumCommand = new RelayCommand(AddExcludedAlbumAction);
        RemoveExcludedAlbumCommand = new RelayCommandParams(RemoveExcludedAlbumAction);
        TestMarginCommand = new RelayCommand(TestMarginAction);

        AlbumList = new ObservableCollection<ListItem>(Settings.Albums.Select(x => new ListItem(x.ToString())));
        ExcludedAlbumList = new ObservableCollection<ListItem>(Settings.ExcludedAlbums.Select(x => new ListItem(x.ToString())));
        CancelVisible = cancelEnabled;
        Disposed += (_, _) => Dispose();
        ResetPeopleFromSettings();
        _ = LoadPeopleAsync();
    }

    private async Task LoadPeopleAsync()
    {
        if (!await peopleLoadGate.WaitAsync(0))
            return;

        var includedIds = GetPeopleInState(PersonSelectionState.Included).ToList();
        var excludedIds = GetPeopleInState(PersonSelectionState.Excluded).ToList();
        var loadCancellation = thumbnailLoadCancellation.Token;

        try
        {
            if (string.IsNullOrWhiteSpace(Settings.ImmichServerUrl) || string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                PeopleStatus = "Enter the server URL and API key, then tap Refresh people.";
                return;
            }

            IsPeopleLoading = true;
            PeopleStatus = "Loading people…";
            var logic = new ImmichFrameLogic(Settings);
            allPeople = await Task.Run(
                () => logic.GetPeopleAsync(loadCancellation),
                loadCancellation);
            loadCancellation.ThrowIfCancellationRequested();
            if (Volatile.Read(ref disposed) != 0)
                return;

            peopleLogic = logic;
            ResetThumbnailLoading();
            RebuildPeopleLists(includedIds, excludedIds);
            UpdatePeopleStatus();
        }
        catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
        {
            // The settings view was closed or its people list was replaced.
        }
        catch (Exception ex)
        {
            PeopleStatus = $"Could not load people: {ex.Message}";
        }
        finally
        {
            IsPeopleLoading = false;
            peopleLoadGate.Release();
        }
    }

    private void ResetPeopleFromSettings()
    {
        RebuildPeopleLists(Settings.People, Settings.ExcludedPeople);
    }

    private void RebuildPeopleLists(IEnumerable<Guid> includedIds, IEnumerable<Guid> excludedIds)
    {
        DisposePersonItems();
        SelectedAvailablePerson = null;
        SelectedIncludedPerson = null;
        SelectedExcludedPerson = null;
        if (SelectedUnnamedPerson is not null)
            SelectedUnnamedPerson.IsSelected = false;
        SelectedUnnamedPerson = null;
        UnnamedSelectionStatus = "Tap an unnamed face to change its filter.";

        var includedIdSet = includedIds.Distinct().ToHashSet();
        var excludedIdSet = excludedIds
            .Where(id => !includedIdSet.Contains(id))
            .Distinct()
            .ToHashSet();
        var resolvedIds = allPeople.Select(person => person.Id).ToHashSet();
        var orderedItems = allPeople
            .Select((person, index) => new PersonListItem(
                person.Id,
                person.Name,
                true,
                GetSelectionState(person.Id, includedIdSet, excludedIdSet),
                index))
            .ToList();

        var unresolvedItems = includedIdSet
            .Concat(excludedIdSet)
            .Where(id => !resolvedIds.Contains(id))
            .Distinct()
            .Select((id, index) => new PersonListItem(
                id,
                "Saved person (name unavailable)",
                false,
                includedIdSet.Contains(id) ? PersonSelectionState.Included : PersonSelectionState.Excluded,
                allPeople.Count + index));
        orderedItems.AddRange(unresolvedItems);

        var namedItems = orderedItems.Where(person => !person.IsUnnamedGridCandidate);
        AvailablePeople = ToOrderedCollection(namedItems.Where(person => person.SelectionState == PersonSelectionState.Available));
        IncludedPeople = ToOrderedCollection(namedItems.Where(person => person.SelectionState == PersonSelectionState.Included));
        ExcludedPeople = ToOrderedCollection(namedItems.Where(person => person.SelectionState == PersonSelectionState.Excluded));
        UnnamedPeopleRows = PersonGridRow.Create(
            orderedItems.Where(person => person.IsUnnamedGridCandidate),
            UnnamedPeoplePerRow);
    }

    private static PersonSelectionState GetSelectionState(
        Guid id,
        IReadOnlySet<Guid> includedIds,
        IReadOnlySet<Guid> excludedIds)
    {
        if (includedIds.Contains(id))
            return PersonSelectionState.Included;

        return excludedIds.Contains(id)
            ? PersonSelectionState.Excluded
            : PersonSelectionState.Available;
    }

    private static ObservableCollection<PersonListItem> ToOrderedCollection(IEnumerable<PersonListItem> people)
    {
        return new ObservableCollection<PersonListItem>(people.OrderBy(person => person.SortIndex));
    }

    private void IncludeSelectedPerson()
    {
        MoveAvailablePerson(SelectedAvailablePerson, IncludedPeople, ExcludedPeople);
        SelectedIncludedPerson = SelectedAvailablePerson;
        SelectedAvailablePerson = null;
    }

    private void ExcludeSelectedPerson()
    {
        MoveAvailablePerson(SelectedAvailablePerson, ExcludedPeople, IncludedPeople);
        SelectedExcludedPerson = SelectedAvailablePerson;
        SelectedAvailablePerson = null;
    }

    private void MoveAvailablePerson(PersonListItem? person, ObservableCollection<PersonListItem> destination, ObservableCollection<PersonListItem> otherList)
    {
        if (person == null)
            return;

        AvailablePeople.Remove(person);
        RemoveById(otherList, person.Id);
        person.SelectionState = ReferenceEquals(destination, IncludedPeople)
            ? PersonSelectionState.Included
            : PersonSelectionState.Excluded;
        AddOrdered(destination, person);
        UpdatePeopleStatus();
    }

    private void RemoveSelectedIncludedPerson()
    {
        ReturnToAvailable(IncludedPeople, SelectedIncludedPerson);
        SelectedIncludedPerson = null;
    }

    private void RemoveSelectedExcludedPerson()
    {
        ReturnToAvailable(ExcludedPeople, SelectedExcludedPerson);
        SelectedExcludedPerson = null;
    }

    private void ReturnToAvailable(ObservableCollection<PersonListItem> source, PersonListItem? person)
    {
        if (person == null)
            return;

        source.Remove(person);
        person.SelectionState = PersonSelectionState.Available;
        if (allPeople.Any(candidate => candidate.Id == person.Id))
            AddOrdered(AvailablePeople, person);
        UpdatePeopleStatus();
    }

    private static void AddOrdered(ObservableCollection<PersonListItem> collection, PersonListItem person)
    {
        if (collection.Any(existing => existing.Id == person.Id))
            return;

        var index = collection.TakeWhile(existing => existing.SortIndex <= person.SortIndex).Count();
        collection.Insert(index, person);
    }

    private static void RemoveById(ObservableCollection<PersonListItem> collection, Guid id)
    {
        var existing = collection.FirstOrDefault(person => person.Id == id);
        if (existing != null)
            collection.Remove(existing);
    }

    private void UpdatePeopleStatus()
    {
        var unnamedPeople = UnnamedPeopleRows.SelectMany(row => row.People).ToList();
        var availableCount = AvailablePeople.Count +
            unnamedPeople.Count(person => person.SelectionState == PersonSelectionState.Available);
        var includedCount = IncludedPeople.Count +
            unnamedPeople.Count(person => person.SelectionState == PersonSelectionState.Included);
        var excludedCount = ExcludedPeople.Count +
            unnamedPeople.Count(person => person.SelectionState == PersonSelectionState.Excluded);
        PeopleStatus = $"{availableCount} available · {includedCount} included · {excludedCount} excluded";
    }

    private IEnumerable<Guid> GetPeopleInState(PersonSelectionState state)
    {
        var namedPeople = state switch
        {
            PersonSelectionState.Included => IncludedPeople,
            PersonSelectionState.Excluded => ExcludedPeople,
            _ => AvailablePeople,
        };

        return namedPeople
            .Select(person => person.Id)
            .Concat(UnnamedPeopleRows
                .SelectMany(row => row.People)
                .Where(person => person.SelectionState == state)
                .Select(person => person.Id));
    }

    private void SelectUnnamedPerson(object value)
    {
        if (value is not PersonListItem person)
            return;

        if (SelectedUnnamedPerson is not null && !ReferenceEquals(SelectedUnnamedPerson, person))
            SelectedUnnamedPerson.IsSelected = false;

        SelectedUnnamedPerson = person;
        person.IsSelected = true;
        UpdateUnnamedSelectionStatus();
    }

    private void SetSelectedUnnamedState(PersonSelectionState state)
    {
        if (SelectedUnnamedPerson is null)
            return;

        SelectedUnnamedPerson.SelectionState = state;
        UpdateUnnamedSelectionStatus();
        UpdatePeopleStatus();
    }

    private void UpdateUnnamedSelectionStatus()
    {
        UnnamedSelectionStatus = SelectedUnnamedPerson is null
            ? "Tap an unnamed face to change its filter."
            : $"Selected face · {SelectedUnnamedPerson.SelectionLabel}";
    }

    public async Task LoadPersonThumbnailAsync(PersonListItem person)
    {
        var logic = peopleLogic;
        var cancellation = thumbnailLoadCancellation;
        if (Volatile.Read(ref disposed) != 0 ||
            logic is null ||
            !person.TryBeginThumbnailLoad(out var requestVersion))
        {
            return;
        }

        var enteredGate = false;
        Bitmap? thumbnail = null;
        try
        {
            await thumbnailLoadGate.WaitAsync(cancellation.Token);
            enteredGate = true;
            var thumbnailBytes = await logic.GetPersonThumbnailAsync(
                person.Id,
                cancellation.Token);

            if (thumbnailBytes is null || thumbnailBytes.Length == 0)
            {
                person.MarkThumbnailUnavailable(requestVersion);
                return;
            }

            thumbnail = await Task.Run(() =>
            {
                using var encodedThumbnail = new MemoryStream(thumbnailBytes);
                return Bitmap.DecodeToWidth(
                    encodedThumbnail,
                    PersonThumbnailDecodeWidth,
                    BitmapInterpolationMode.MediumQuality);
            }, cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                if (person.TrySetThumbnail(thumbnail, requestVersion))
                    thumbnail = null;
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The settings view or people list was replaced.
        }
        catch
        {
            person.MarkThumbnailUnavailable(requestVersion);
        }
        finally
        {
            thumbnail?.Dispose();
            if (enteredGate)
                thumbnailLoadGate.Release();
        }
    }

    public void ReleasePersonThumbnail(PersonListItem person)
    {
        person.ReleaseThumbnail();
    }

    private void ResetThumbnailLoading()
    {
        var previousCancellation = thumbnailLoadCancellation;
        thumbnailLoadCancellation = new CancellationTokenSource();
        previousCancellation.Cancel();
        previousCancellation.Dispose();
    }

    private void DisposePersonItems()
    {
        AvailablePeople
            .Concat(IncludedPeople)
            .Concat(ExcludedPeople)
            .Concat(UnnamedPeopleRows.SelectMany(row => row.People))
            .DistinctBy(person => person.Id)
            .ToList()
            .ForEach(person => person.Dispose());
    }

    private void TestMarginAction() => UpdateMargin(Settings.Margin);

    private void AddAlbumAction() => AlbumList.Add(new ListItem());

    private void RemoveAlbumAction(object param)
    {
        var item = AlbumList.FirstOrDefault(x => x.Id == Guid.Parse(param.ToString()!));
        if (item != null)
            AlbumList.Remove(item);
    }

    private void AddExcludedAlbumAction() => ExcludedAlbumList.Add(new ListItem());

    private void RemoveExcludedAlbumAction(object param)
    {
        var item = ExcludedAlbumList.FirstOrDefault(x => x.Id == Guid.Parse(param.ToString()!));
        if (item != null)
            ExcludedAlbumList.Remove(item);
    }

    private void CancelAction()
    {
        try
        {
            Settings.ReloadFromJson();
            Navigate(new MainViewModel());
        }
        catch (SettingsNotValidException)
        {
            Navigate(new ErrorViewModel(new Exception("Please provide valid settings")));
        }
    }

    private void SaveAction()
    {
        try
        {
            Settings.People = GetPeopleInState(PersonSelectionState.Included).ToList();
            Settings.ExcludedPeople = GetPeopleInState(PersonSelectionState.Excluded).ToList();
            Settings.Albums = AlbumList.Select(x => Guid.Parse(x.Value)).ToList();
            Settings.ExcludedAlbums = ExcludedAlbumList.Select(x => Guid.Parse(x.Value)).ToList();
            if (string.IsNullOrEmpty(Settings.ImmichServerUrl) || string.IsNullOrEmpty(Settings.ApiKey))
                return;

            Settings.SaveSettings(Settings);
            Settings = Settings.CurrentSettings;
        }
        catch (Exception ex)
        {
            Navigate(new ErrorViewModel(ex));
            return;
        }

        Navigate(new MainViewModel());
    }

    private void QuitAction() => Environment.Exit(0);

    private async void BackupAction()
    {
        var backupFile = await ShowSaveFileDialog(true);
        if (backupFile is not null)
            await Settings.BackupSettings(backupFile);
    }

    private async void RestoreAction()
    {
        var restoreFile = await ShowOpenFileDialog();
        if (restoreFile is null)
            return;

        await Settings.RestoreSettings(restoreFile);
        Settings = Settings.CurrentSettings;
        AlbumList = new ObservableCollection<ListItem>(Settings.Albums.Select(x => new ListItem(x.ToString())));
        ExcludedAlbumList = new ObservableCollection<ListItem>(Settings.ExcludedAlbums.Select(x => new ListItem(x.ToString())));
        ResetPeopleFromSettings();
        _ = LoadPeopleAsync();
    }

    public async Task<IStorageFile?> ShowSaveFileDialog(bool showOverwritePrompt)
    {
        var topLevel = TopLevel.GetTopLevel(GetUserControl!());
        if (topLevel == null)
            return null;

        return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = "ImmichFrameSettings.json",
            ShowOverwritePrompt = showOverwritePrompt
        });
    }

    public async Task<IStorageFile?> ShowOpenFileDialog()
    {
        var topLevel = TopLevel.GetTopLevel(GetUserControl!());
        if (topLevel == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false,
            SuggestedFileName = "ImmichFrameSettings.json",
        });
        return files.FirstOrDefault();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        thumbnailLoadCancellation.Cancel();
        DisposePersonItems();
        peopleLogic = null;
    }
}

public sealed class PersonListItem : ObservableObject, IDisposable
{
    private Bitmap? thumbnail;
    private PersonSelectionState selectionState;
    private bool isSelected;
    private int thumbnailLoadStarted;
    private int thumbnailRequestVersion;
    private int disposed;

    public PersonListItem(
        Guid id,
        string? name,
        bool isResolved,
        PersonSelectionState selectionState = PersonSelectionState.Available,
        int sortIndex = int.MaxValue)
    {
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(name) ? "Unnamed person" : name;
        Detail = isResolved
            ? string.IsNullOrWhiteSpace(name) ? "No name in Immich" : string.Empty
            : "Saved person · name unavailable";
        Initials = CreateInitials(name);
        IsUnnamedGridCandidate = isResolved && string.IsNullOrWhiteSpace(name);
        this.selectionState = selectionState;
        SortIndex = sortIndex;
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public string Detail { get; }
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
    public string Initials { get; }
    public bool IsUnnamedGridCandidate { get; }
    public int SortIndex { get; }
    public PersonSelectionState SelectionState
    {
        get => selectionState;
        set
        {
            if (SetProperty(ref selectionState, value))
                OnPropertyChanged(nameof(SelectionLabel));
        }
    }
    public string SelectionLabel => SelectionState switch
    {
        PersonSelectionState.Included => "Included",
        PersonSelectionState.Excluded => "Excluded",
        _ => "Available",
    };
    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
    public Bitmap? Thumbnail
    {
        get => thumbnail;
        private set
        {
            if (SetProperty(ref thumbnail, value))
                OnPropertyChanged(nameof(IsThumbnailPlaceholderVisible));
        }
    }
    public bool IsThumbnailPlaceholderVisible => Thumbnail is null;

    internal bool TryBeginThumbnailLoad(out int requestVersion)
    {
        requestVersion = Volatile.Read(ref thumbnailRequestVersion);
        return Volatile.Read(ref disposed) == 0 &&
            Interlocked.Exchange(ref thumbnailLoadStarted, 1) == 0;
    }

    internal bool TrySetThumbnail(Bitmap value, int requestVersion)
    {
        if (Volatile.Read(ref disposed) != 0 ||
            requestVersion != Volatile.Read(ref thumbnailRequestVersion))
        {
            return false;
        }

        var previous = Thumbnail;
        Thumbnail = value;
        previous?.Dispose();
        return true;
    }

    internal void MarkThumbnailUnavailable(int requestVersion)
    {
        if (Volatile.Read(ref disposed) == 0 &&
            requestVersion == Volatile.Read(ref thumbnailRequestVersion))
            OnPropertyChanged(nameof(IsThumbnailPlaceholderVisible));
    }

    internal void ReleaseThumbnail()
    {
        if (Volatile.Read(ref disposed) != 0)
            return;

        Interlocked.Increment(ref thumbnailRequestVersion);
        Interlocked.Exchange(ref thumbnailLoadStarted, 0);
        var previous = Thumbnail;
        Thumbnail = null;
        previous?.Dispose();
    }

    private static string CreateInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        return string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        var previous = Thumbnail;
        Thumbnail = null;
        previous?.Dispose();
    }
}

public enum PersonSelectionState
{
    Available,
    Included,
    Excluded,
}

public sealed class PersonGridRow
{
    private PersonGridRow(IReadOnlyList<PersonListItem> people)
    {
        People = people;
    }

    public IReadOnlyList<PersonListItem> People { get; }

    public static ObservableCollection<PersonGridRow> Create(
        IEnumerable<PersonListItem> people,
        int peoplePerRow)
    {
        if (peoplePerRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(peoplePerRow));

        var rows = people
            .Select((person, index) => new { person, index })
            .GroupBy(item => item.index / peoplePerRow)
            .Select(group => new PersonGridRow(group.Select(item => item.person).ToList()));
        return new ObservableCollection<PersonGridRow>(rows);
    }
}

public class ListItem
{
    public ListItem(string value = "")
    {
        Id = Guid.NewGuid();
        Value = value;
    }

    public Guid Id { get; set; }
    public string Value { get; set; }
}
