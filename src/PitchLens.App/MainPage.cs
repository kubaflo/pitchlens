using Microsoft.AspNetCore.Components.WebView.Maui;
#if PITCHLENS_DUO
using Microsoft.Maui.Controls.Foldable;
#endif

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
#if PITCHLENS_DUO
        Content = new TwoPaneView
        {
            AutomationId = "duo-host",
            MinWideModeWidth = double.MaxValue,
            MinTallModeHeight = double.MaxValue,
            Pane1 = web,
            Pane2 = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 24,
                    Spacing = 16,
                    Children =
                    {
                        new Label { Text = "PitchLens", FontSize = 32, TextColor = Colors.White },
                        new Label { Text = "An uninterrupted replay.", FontSize = 24, TextColor = Colors.White },
                        new Label
                        {
                            Text = "The recorded pitch, controls and evidence stay together on one side of the active fold. Open flat to return to the full canvas.",
                            FontSize = 18,
                            TextColor = Colors.White
                        },
                        new Label
                        {
                            Text = "Synthetic football. Recorded analysis. Fresh AI is unavailable on iOS.",
                            FontSize = 16,
                            TextColor = Colors.White
                        }
                    }
                }
            }
        };
#else
        Content = web;
#endif
    }
}
