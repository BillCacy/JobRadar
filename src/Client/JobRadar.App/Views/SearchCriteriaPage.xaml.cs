using JobRadar.App.ViewModels;
using JobRadar.Contracts.Models;

namespace JobRadar.App.Views;

public partial class SearchCriteriaPage : ContentPage
{
    private readonly SearchCriteriaViewModel _viewModel;

    public SearchCriteriaPage(SearchCriteriaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    // Tap-to-delete instead of a swipe/button-per-row binding back to the page's ViewModel -
    // keeps the DataTemplate's binding context simple (just the SearchCriteria itself).
    private async void OnSavedSearchSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchCriteria selected)
            return;

        var confirmed = await DisplayAlert("Remove search", $"Stop watching '{selected.Keywords}'?", "Remove", "Cancel");
        if (confirmed)
            await _viewModel.DeleteCommand.ExecuteAsync(selected);

        ((CollectionView)sender!).SelectedItem = null;
    }
}
