using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using ImmichFrame.Core.Exceptions;
using ImmichFrame.Core.Logic;
using ImmichFrame.Core.Models;
using ImmichFrame.Helpers;
using ImmichFrame.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImmichFrame.ViewModels;

public partial class SettingsViewModel : NavigatableViewModelBase
{
    private readonly SemaphoreSlim peopleLoadGate = new(1, 1);
    private IReadOnlyList<PersonInfo> allPeople = Array.Empty<PersonInfo>();

    [ObservableProperty]
    private ObservableCollection<PersonListItem> availablePeople = new();

    [ObservableProperty]
    private ObservableCollection<PersonListItem> includedPeople = new();

    [ObservableProperty]
    private ObservableCollection<PersonListItem> excludedPeople = new();

    [ObservableProperty]
    private PersonListItem? selectedAvailablePerson;

    [ObservableProperty]
    private PersonListItem? selectedIncludedPerson;

    [ObservableProperty]
    private PersonListItem? selectedExcludedPerson;

    [ObservableProperty]
    private string peopleStatus = "Loading people names…";

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
    public ICommand AddAlbumCommand { get; }
    public ICommand RemoveAlbumCommand { get; }
    public ICommand AddExcludedAlbumCommand { get; }
    public ICommand RemoveExcludedAlbumCommand { get; }
    public ICommand TestMarginCommand { get; }
    public List<string> StretchOptions { get; } = Enum.GetNames(typeof(Stretch)).ToList();
    public IReadOnlyList<string> LetterboxBackgroundChoices { get; } = LetterboxBackgroundOptions.All;
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
        AddAlbumCommand = new RelayCommand(AddAlbumAction);
        RemoveAlbumCommand = new RelayCommandParams(RemoveAlbumAction);
        AddExcludedAlbumCommand = new RelayCommand(AddExcludedAlbumAction);
        RemoveExcludedAlbumCommand = new RelayCommandParams(RemoveExcludedAlbumAction);
        TestMarginCommand = new RelayCommand(TestMarginAction);

        AlbumList = new ObservableCollection<ListItem>(Settings.Albums.Select(x => new ListItem(x.ToString())));
        ExcludedAlbumList = new ObservableCollection<ListItem>(Settings.ExcludedAlbums.Select(x => new ListItem(x.ToString())));
        CancelVisible = cancelEnabled;
        ResetPeopleFromSettings();
        _ = LoadPeopleAsync();
    }

    private async Task LoadPeopleAsync()
    {
        if (!await peopleLoadGate.WaitAsync(0))
            return;

        var includedIds = IncludedPeople.Select(person => person.Id).ToList();
        var excludedIds = ExcludedPeople.Select(person => person.Id).ToList();

        try
        {
            if (string.IsNullOrWhiteSpace(Settings.ImmichServerUrl) || string.IsNullOrWhiteSpace(Settings.ApiKey))
            {
                PeopleStatus = "Enter the server URL and API key, then tap Refresh names.";
                return;
            }

            IsPeopleLoading = true;
            PeopleStatus = "Loading people names…";
            var logic = new ImmichFrameLogic(Settings);
            allPeople = await Task.Run(() => logic.GetPeopleAsync());
            RebuildPeopleLists(includedIds, excludedIds);
            UpdatePeopleStatus();
        }
        catch (Exception ex)
        {
            PeopleStatus = $"Could not load names: {ex.Message}";
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
        var peopleById = allPeople.ToDictionary(person => person.Id);
        var included = includedIds.Distinct().Select(id => CreatePersonListItem(id, peopleById)).ToList();
        var excludedIdSet = excludedIds.Except(included.Select(person => person.Id)).ToHashSet();
        var excluded = excludedIdSet.Select(id => CreatePersonListItem(id, peopleById)).ToList();
        var selectedIds = included.Select(person => person.Id).Concat(excluded.Select(person => person.Id)).ToHashSet();
        var available = allPeople
            .Where(person => !selectedIds.Contains(person.Id))
            .Select(person => new PersonListItem(person.Id, person.Name, true));

        IncludedPeople = ToSortedCollection(included);
        ExcludedPeople = ToSortedCollection(excluded);
        AvailablePeople = ToSortedCollection(available);
    }

    private static PersonListItem CreatePersonListItem(Guid id, IReadOnlyDictionary<Guid, PersonInfo> peopleById)
    {
        return peopleById.TryGetValue(id, out var person)
            ? new PersonListItem(id, person.Name, true)
            : new PersonListItem(id, "Saved person (name unavailable)", false);
    }

    private static ObservableCollection<PersonListItem> ToSortedCollection(IEnumerable<PersonListItem> people)
    {
        return new ObservableCollection<PersonListItem>(people.OrderBy(person => person.DisplayName, StringComparer.CurrentCultureIgnoreCase));
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
        AddSorted(destination, person);
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
        if (allPeople.Any(candidate => candidate.Id == person.Id))
            AddSorted(AvailablePeople, person);
        UpdatePeopleStatus();
    }

    private static void AddSorted(ObservableCollection<PersonListItem> collection, PersonListItem person)
    {
        if (collection.Any(existing => existing.Id == person.Id))
            return;

        var index = collection.TakeWhile(existing => StringComparer.CurrentCultureIgnoreCase.Compare(existing.DisplayName, person.DisplayName) <= 0).Count();
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
        PeopleStatus = $"{AvailablePeople.Count} available · {IncludedPeople.Count} included · {ExcludedPeople.Count} excluded";
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
            Settings.People = IncludedPeople.Select(x => x.Id).ToList();
            Settings.ExcludedPeople = ExcludedPeople.Select(x => x.Id).ToList();
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
}

public sealed class PersonListItem
{
    public PersonListItem(Guid id, string? name, bool isResolved)
    {
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(name) ? "Unnamed person" : name;
        Detail = isResolved ? string.Empty : id.ToString();
    }

    public Guid Id { get; }
    public string DisplayName { get; }
    public string Detail { get; }
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
