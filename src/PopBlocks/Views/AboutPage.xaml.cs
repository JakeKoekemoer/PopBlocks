using PopBlocks.Engine.Services;
using PopBlocks.Graphics;

namespace PopBlocks.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        LogoView.Drawable = new LogoDrawable();

        // Read from the installed package, so it always matches what was actually built.
        string version = AppInfo.Current.VersionString;
        string build = AppInfo.Current.BuildString;
        VersionLabel.Text = $"VERSION {version}  ·  BUILD {build}";
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
