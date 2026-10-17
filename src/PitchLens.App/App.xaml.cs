namespace PitchLens.App;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new ReplayPage();
        var window = new Window(page)
        {
            Title = "PitchLens | Synthetic match lab",
            Width = 1280,
            Height = 860,
            MinimumWidth = 640,
            MinimumHeight = 640
        };
        window.Deactivated += (_, _) => page.Suspend();
        window.Stopped += (_, _) => page.Suspend();
        window.Destroying += (_, _) => page.Dispose();
        return window;
    }
}
