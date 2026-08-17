using JobRadar.App.Services;
using JobRadar.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var session = _services.GetRequiredService<SessionService>();

		// A remembered UserId (see SessionService) means we're already "logged in" - skip
		// straight to the tabbed shell instead of making the user re-enter their email.
		Page rootPage = session.UserId is not null
			? new AppShell()
			: _services.GetRequiredService<LoginPage>();

		return new Window(rootPage);
	}
}
