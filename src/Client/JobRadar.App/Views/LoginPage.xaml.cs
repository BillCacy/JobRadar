using JobRadar.App.Services;

namespace JobRadar.App.Views;

public partial class LoginPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly SessionService _session;

    public LoginPage(ApiClient api, SessionService session)
    {
        InitializeComponent();
        _api = api;
        _session = session;
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorLabel.Text = "Email is required.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        Spinner.IsVisible = true;
        Spinner.IsRunning = true;

        try
        {
            var user = await _api.RegisterAsync(email, email);
            _session.UserId = user.Id;

            // Swap the whole root page rather than pushing/popping - there's nothing on the
            // login stack worth navigating back to once you're "signed in".
            Application.Current!.MainPage = new AppShell();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Couldn't reach the server: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
        }
    }
}
