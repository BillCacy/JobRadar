using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobRadar.App.Services;
using JobRadar.Contracts.Models;

namespace JobRadar.App.ViewModels;

public sealed partial class SearchCriteriaViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly SessionService _session;

    public ObservableCollection<SearchCriteria> SavedSearches { get; } = new();

    [ObservableProperty] private string keywords = string.Empty;
    [ObservableProperty] private string location = string.Empty;

    // Kept as text (not decimal?) so the two-way Entry binding never has to guess how to
    // convert an empty/partial string mid-typing - it's parsed once, in SaveAsync.
    [ObservableProperty] private string minSalaryText = string.Empty;

    [ObservableProperty] private bool remoteOnly;
    [ObservableProperty] private string? errorMessage;

    public SearchCriteriaViewModel(ApiClient api, SessionService session)
    {
        _api = api;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_session.UserId is not { } userId)
            return;

        var criteria = await _api.GetCriteriaAsync(userId);
        SavedSearches.Clear();
        foreach (var c in criteria)
            SavedSearches.Add(c);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_session.UserId is not { } userId)
            return;
        if (string.IsNullOrWhiteSpace(Keywords))
        {
            ErrorMessage = "Keywords is required.";
            return;
        }

        decimal? minSalary = null;
        if (!string.IsNullOrWhiteSpace(MinSalaryText))
        {
            if (!decimal.TryParse(MinSalaryText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                ErrorMessage = "Minimum salary must be a number.";
                return;
            }
            minSalary = parsed;
        }

        ErrorMessage = null;

        // JobType/ExcludeKeywords aren't exposed in this simple form yet - default to "Any" /
        // none. Wiring a picker + a chip-entry control for those is the natural next feature.
        await _api.CreateCriteriaAsync(userId, new CreateCriteriaRequest(
            Keywords.Trim(), Location.Trim(), minSalary, RemoteOnly, JobTypeFilter.Any, null));

        Keywords = string.Empty;
        Location = string.Empty;
        MinSalaryText = string.Empty;
        RemoteOnly = false;

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(SearchCriteria criteria)
    {
        if (_session.UserId is not { } userId)
            return;

        await _api.DeleteCriteriaAsync(userId, criteria.Id);
        SavedSearches.Remove(criteria);
    }
}
