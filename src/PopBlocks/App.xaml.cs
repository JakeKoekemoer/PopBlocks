using Microsoft.Extensions.DependencyInjection;
using PopBlocks.Views;

namespace PopBlocks;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Pages are NOT constructor parameters: the DI container would build them before this constructor
    // runs, i.e. before App.xaml's colours and styles exist, and every StaticResource lookup would fail.
    public App(IServiceProvider services)
    {
        Diagnostics.Crumb("App ctor: start");
        _services = services;
        InitializeComponent();
        Diagnostics.Crumb("App ctor: resources loaded");

        // The game's look is dark-navy throughout, so keep system chrome (status bar icons) in dark mode too.
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Diagnostics.Crumb("App.CreateWindow");
        var mainPage = _services.GetRequiredService<MainPage>();
        return new Window(new NavigationPage(mainPage));
    }
}
