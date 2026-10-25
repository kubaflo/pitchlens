#if PITCHLENS_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Blazor;
#endif

namespace PitchLens.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<MatchService>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
#if PITCHLENS_DEVFLOW
        builder.AddMauiDevFlowAgent();
        builder.AddMauiBlazorDevFlowTools();
#endif
        return builder.Build();
    }
}
