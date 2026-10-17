using System.Globalization;
using Microsoft.Maui.Dispatching;
using PitchLens.Core;

namespace PitchLens.App;

public sealed class ReplayPage : ContentPage, IDisposable
{
    private readonly Match match = DemoMatch.Create();
    private readonly Playback playback = new(DemoMatch.Duration, new MonotonicClock());
    private readonly PitchDrawable drawable;
    private readonly GraphicsView pitch;
    private readonly Label scoreboard = new() { FontSize = 25, FontAttributes = FontAttributes.Bold };
    private readonly Label clock = new();
    private readonly Label stats = new() { LineHeight = 1.4 };
    private readonly Label possession = new();
    private readonly Label teamStats = new() { FontSize = 13, LineHeight = 1.3 };
    private readonly Label emptyInsights = new() { Text = "Play or seek to reveal verified events. No future events are shown." };
    private readonly Button play = new() { Text = "Play", AutomationId = "play-pause" };
    private readonly Slider timeline = new() { Minimum = 0, Maximum = DemoMatch.Duration, AutomationId = "timeline" };
    private readonly Picker roster = new() { Title = "Select player", TextColor = Colors.White, AutomationId = "roster" };
    private readonly Picker mode = new() { Title = "Explanation mode", TextColor = Colors.White, AutomationId = "mode" };
    private readonly VerticalStackLayout insights = new() { Spacing = 10 };
    private readonly Grid main = new() { ColumnSpacing = 24, RowSpacing = 20 };
    private readonly VerticalStackLayout sidebar;
    private readonly VerticalStackLayout replay;
    private IDispatcherTimer? timer;
    private string selectedId = "h3";
    private bool updatingTimeline, disposed;
    private string insightKey = "";
    private bool? wideLayout;

    public ReplayPage()
    {
        Title = "PitchLens";
        BackgroundColor = Color.FromArgb("#102A36");
        drawable = new(match) { SelectedId = selectedId };
        pitch = new GraphicsView { Drawable = drawable, HeightRequest = 460, AutomationId = "pitch" };
        SemanticProperties.SetDescription(pitch, "Synthetic football replay. Use the player picker for accessible selection.");
        pitch.StartInteraction += OnPitchInteraction;
        roster.ItemsSource = match.Players.Select(p =>
            $"{match.Teams.Single(t => t.Id == p.TeamId).Name} | #{p.Number} {p.Name}").ToList();
        roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == selectedId);
        SemanticProperties.SetDescription(roster, "Selected player, alternative to clicking the pitch");
        roster.SelectedIndexChanged += (_, _) =>
        {
            if (roster.SelectedIndex < 0) return;
            selectedId = match.Players[roster.SelectedIndex].Id;
            Render();
        };
        mode.ItemsSource = new[] { "Casual", "Analyst" };
        mode.SelectedIndex = 0;
        mode.SelectedIndexChanged += (_, _) => Render();
        SemanticProperties.SetDescription(mode, "Scripted explanation detail");
        SemanticProperties.SetDescription(timeline, "Replay time in seconds, zero to seventy-two");
        timeline.ValueChanged += (_, e) =>
        {
            if (updatingTimeline) return;
            playback.Seek(e.NewValue);
            Render();
        };
        play.Clicked += (_, _) =>
        {
            if (playback.IsPlaying) Suspend();
            else
            {
                playback.Play();
                if (playback.IsPlaying) timer?.Start();
                Render();
            }
        };
        var restart = new Button { Text = "Restart", AutomationId = "restart" };
        restart.Clicked += (_, _) => { Suspend(); playback.Restart(); Render(); };
        var back = new Button { Text = "-5 s", AutomationId = "seek-back" };
        back.Clicked += (_, _) => SeekBy(-5);
        var forward = new Button { Text = "+5 s", AutomationId = "seek-forward" };
        forward.Clicked += (_, _) => SeekBy(5);
        var controls = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var button in new[] { play, restart, back, forward })
        {
            button.Margin = new Thickness(0, 0, 8, 8);
            controls.Children.Add(button);
        }
        replay = new VerticalStackLayout
        {
            Spacing = 12,
            Children = { scoreboard, teamStats, possession, pitch, timeline, clock, controls,
                new Label { Text = "Harbor: cyan | Orchard: peach | Ball: white\nClick a player or use the picker. Gold = selection and past movement.",
                    FontSize = 13, TextColor = Color.FromArgb("#ADC5CE") } }
        };
        sidebar = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Heading("PLAYER LENS"), roster, stats, Heading("SCRIPTED EXPLANATIONS"), mode,
                new Label { Text = "Event facts only. Not generated AI.", FontSize = 13, TextColor = Color.FromArgb("#FFE39A") },
                emptyInsights, insights
            }
        };
        main.Children.Add(replay);
        main.Children.Add(sidebar);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                Children =
                {
                    new Label { Text = "PITCHLENS / MATCH LAB", FontSize = 15, CharacterSpacing = 3,
                        TextColor = Color.FromArgb("#89E6ED") },
                    new Label { Text = "Read the game. Rewind the evidence.", FontSize = 30, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "SYNTHETIC 6v6 DEMO | 72 seconds | Fixed seed 2026 | Local only",
                        FontSize = 13, TextColor = Color.FromArgb("#FFE39A") },
                    main
                }
            }
        };
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Render();
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        FontSize = 13,
        CharacterSpacing = 2,
        TextColor = Color.FromArgb("#89E6ED")
    };

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (disposed || timer is not null) return;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += OnTick;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        Suspend();
        ReleaseTimer();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Render();
        if (!playback.IsPlaying) timer?.Stop();
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var wide = Width >= 1000;
        pitch.HeightRequest = Math.Clamp((wide ? Width - 440 : Width - 48) * 68 / 105, 280, 560);
        if (wideLayout == wide) return;
        wideLayout = wide;
        main.ColumnDefinitions.Clear();
        main.RowDefinitions.Clear();
        main.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        main.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (wide)
        {
            main.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(320)));
            Grid.SetColumn(sidebar, 1);
            Grid.SetRow(sidebar, 0);
        }
        else
        {
            main.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(sidebar, 0);
            Grid.SetRow(sidebar, 1);
        }
    }

    private void OnPitchInteraction(object? sender, TouchEventArgs e)
    {
        if (pitch.Width <= 0 || pitch.Height <= 0 || e.Touches.Length == 0) return;
        var point = e.Touches[0];
        var time = drawable.Time;
        var id = PitchTransform.Fit(pitch.Width, pitch.Height).HitTest(
            match.Players.Select(p => (p.Id, p.Path.At(time))),
            new Position(point.X, point.Y), PitchDrawable.PlayerRadius);
        if (id is not null)
            roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == id);
    }

    private void SeekBy(double delta)
    {
        playback.Seek(playback.Position + delta);
        Render();
        if (!playback.IsPlaying) timer?.Stop();
    }

    public void Suspend()
    {
        playback.Pause();
        timer?.Stop();
        if (!disposed) Render();
    }

    private void Render()
    {
        if (disposed) return;
        var time = playback.Position;
        var snapshot = Replay.At(match, time);
        var selected = match.Player(selectedId);
        var s = snapshot.Players.Single(p => p.PlayerId == selectedId);
        drawable.Time = time;
        drawable.SelectedId = selectedId;
        pitch.Invalidate();
        scoreboard.Text = $"Harbor {snapshot.Score["harbor"]}  :  {snapshot.Score["orchard"]} Orchard";
        teamStats.Text = string.Join("\n", match.Teams.Select(team =>
        {
            var players = snapshot.Players.Where(p => match.Player(p.PlayerId).TeamId == team.Id).ToArray();
            var attempted = players.Sum(p => p.Passes);
            var completed = players.Sum(p => p.CompletedPasses);
            var percent = attempted == 0 ? "unavailable" : (completed / (double)attempted).ToString("P0");
            return $"{team.Name}: passes {completed}/{attempted} ({percent}) | shots {players.Sum(p => p.Shots)}";
        }));
        possession.Text = snapshot.OwnerId is null ? "Ball in transit / restart"
            : $"Possession: {match.Player(snapshot.OwnerId).Name} ({match.Teams.Single(t => t.Id == match.Player(snapshot.OwnerId).TeamId).Name})";
        clock.Text = $"{time:00.0} / {match.Duration:00.0} s" + (time >= match.Duration ? " | Full time" : "");
        play.Text = playback.IsPlaying ? "Pause" : "Play";
        play.IsEnabled = time < match.Duration;
        updatingTimeline = true;
        timeline.Value = time;
        updatingTimeline = false;
        var accuracy = s.PassAccuracy is double value ? value.ToString("P0", CultureInfo.CurrentCulture) : "Unavailable (no attempts)";
        stats.Text = $"{selected.Name} | #{selected.Number}\nPasses: {s.CompletedPasses}/{s.Passes} completed\n" +
            $"Pass accuracy: {accuracy}\nShots: {s.Shots} | Goals: {s.Goals}\n" +
            $"Distance: {s.Distance:F1} m\nMax observed speed: {s.MaxSpeed:F2} m/s\n" +
            "Trajectory-derived to the current replay time.";
        var explanations = Explanations.At(match, time,
            mode.SelectedIndex == 1 ? ExplanationMode.Analyst : ExplanationMode.Casual);
        var key = $"{mode.SelectedIndex}:{string.Join(',', explanations.Select(x => x.EventId))}";
        if (key == insightKey) return;
        insightKey = key;
        insights.Children.Clear();
        emptyInsights.IsVisible = explanations.Count == 0;
        foreach (var explanation in explanations.Reverse())
        {
            var seek = new Button
            {
                Text = $"{explanation.EventId} | {explanation.Time:00.0} s | Replay event",
                HorizontalOptions = LayoutOptions.Start
            };
            seek.Clicked += (_, _) =>
            {
                Suspend();
                playback.Seek(explanation.Time);
                Render();
            };
            insights.Children.Add(new VerticalStackLayout
            {
                Spacing = 5,
                Children = { seek, new Label { Text = explanation.Text, FontSize = 14 } }
            });
        }
    }

    private void ReleaseTimer()
    {
        if (timer is null) return;
        timer.Stop();
        timer.Tick -= OnTick;
        timer = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        Suspend();
        disposed = true;
        ReleaseTimer();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        pitch.StartInteraction -= OnPitchInteraction;
    }
}
