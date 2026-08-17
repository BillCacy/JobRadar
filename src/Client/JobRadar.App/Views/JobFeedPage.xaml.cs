using JobRadar.App.ViewModels;

namespace JobRadar.App.Views;

public partial class JobFeedPage : ContentPage
{
    private readonly JobFeedViewModel _viewModel;

    public JobFeedPage(JobFeedViewModel viewModel)
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
}
