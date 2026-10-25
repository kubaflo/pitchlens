namespace PitchLens.App;

public partial class App : Application
{
    private readonly MatchService service;
    public App(MatchService service)
    {
        this.service = service;
#if !PITCHLENS_DUO
        InitializeComponent();
#endif
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
#if !IOS
        window.Title = "PitchLens";
        window.Width = 1280;
        window.Height = 860;
        window.MinimumWidth = 640;
        window.MinimumHeight = 640;
#endif
        window.Activated += (_, _) => service.Activate();
        window.Deactivated += (_, _) => service.Suspend();
        window.Stopped += (_, _) => service.Suspend();
        window.Destroying += (_, _) => service.Dispose();
        return window;
    }
}
