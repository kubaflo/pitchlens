using Microsoft.AspNetCore.Components.WebView.Maui;

namespace PitchLens.App;

public sealed class MainPage : ContentPage
{
    public MainPage()
    {
#if IOS
        SafeAreaEdges = SafeAreaEdges.All;
        BackgroundColor = Color.FromArgb("#37003C");
#endif
        var web = new BlazorWebView
        {
#if PITCHLENS_DEVFLOW
            HostPage = "wwwroot/devflow.html"
#else
            HostPage = "wwwroot/index.html"
#endif
        };
        web.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Components.Routes) });
        Content = web;
    }
}
