using Microsoft.AspNetCore.Components.WebView.Maui;

namespace PitchLens.App;

public sealed class MainPage : ContentPage
{
    public MainPage()
    {
        var web = new BlazorWebView { HostPage = "wwwroot/index.html" };
        web.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(Components.Routes) });
        Content = web;
    }
}
