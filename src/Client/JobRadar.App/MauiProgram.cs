using JobRadar.App.Services;
using JobRadar.App.ViewModels;
using JobRadar.App.Views;
using Microsoft.Extensions.Logging;

namespace JobRadar.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddSingleton(_ => new ApiClient(GatewayConfig.BaseUrl));
		builder.Services.AddSingleton(_ => new JobHubClient(GatewayConfig.BaseUrl));

		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<JobFeedPage>();
		builder.Services.AddTransient<JobFeedViewModel>();
		builder.Services.AddTransient<SearchCriteriaPage>();
		builder.Services.AddTransient<SearchCriteriaViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
