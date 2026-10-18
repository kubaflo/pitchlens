namespace PitchLens.App;

public partial class App : Application
{
    private readonly MatchService service;
    public App(MatchService service)
    {
        this.service = service;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "PitchLens | Synthetic match lab",
            Width = 1280,
            Height = 860,
            MinimumWidth = 640,
            MinimumHeight = 640
        };
        window.Activated += (_, _) => service.Activate();
        window.Deactivated += (_, _) => service.Suspend();
        window.Stopped += (_, _) => service.Suspend();
        window.Destroying += (_, _) => service.Dispose();
        return window;
    }
}
