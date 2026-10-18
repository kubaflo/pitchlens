using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;
using PitchLens.Core;

namespace PitchLens.App;

public sealed class ReplayPage : ContentPage, IDisposable
{
    private readonly Match match = DemoMatch.Create();
    private readonly Playback playback = new(DemoMatch.Duration, new MonotonicClock());
    private readonly PitchDrawable drawable;
    private readonly GraphicsView pitch;
    private readonly Label scoreboard = new() { FontSize = 22, FontAttributes = FontAttributes.Bold };
    private readonly Label clock = Caption("");
    private readonly Label possession = Caption("");
    private readonly Label teamStats = Caption("");
    private readonly Label identity = new() { FontSize = 22, FontAttributes = FontAttributes.Bold };
    private readonly Label teamIdentity = Caption("");
    private readonly Label shirt = new()
    {
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };
    private readonly Label passes = Value();
    private readonly Label accuracy = Value();
    private readonly Label shots = Value();
    private readonly Label goals = Value();
    private readonly Label distance = Value();
    private readonly Label speed = Value();
    private readonly Label emptyInsights = Caption("No events yet.\nPlay or seek to reveal the match.");
    private readonly Button play = new()
    {
        Text = "Play",
        AutomationId = "play-pause",
        WidthRequest = 80,
        BackgroundColor = Color.FromArgb("#007AFF"),
        TextColor = Colors.White
    };
    private readonly Slider timeline = new() { Minimum = 0, Maximum = DemoMatch.Duration, AutomationId = "timeline" };
    private readonly Picker roster = new() { Title = "Select player", AutomationId = "roster" };
    private readonly Button casual = new() { Text = "Casual", AutomationId = "mode-casual" };
    private readonly Button analyst = new() { Text = "Analyst", AutomationId = "mode-analyst" };
    private readonly VerticalStackLayout insights = new() { Spacing = 14 };
    private readonly Grid main = new() { ColumnSpacing = 16, RowSpacing = 12 };
    private readonly Grid sidebar;
    private readonly Border replayPanel;
    private readonly Border inspectorPanel;
    private readonly Border eventsPanel;
    private readonly Border avatar;
    private readonly Label legend = Caption("Blue: Harbor   Orange: Orchard   White: ball");
    private IDispatcherTimer? timer;
    private string selectedId = "h3";
    private ExplanationMode explanationMode;
    private bool updatingTimeline, disposed;
    private string insightKey = "";
    private bool? wideLayout;

    public ReplayPage()
    {
        Title = "PitchLens";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F5F7"), Color.FromArgb("#1C1C1E"));
        drawable = new(match) { SelectedId = selectedId };
        pitch = new GraphicsView { Drawable = drawable, AutomationId = "pitch", MinimumHeightRequest = 100 };
        SemanticProperties.SetDescription(pitch, "Synthetic football replay. Use the player picker for accessible selection.");
        pitch.StartInteraction += OnPitchInteraction;
        roster.ItemsSource = match.Players.Select(p =>
            $"{match.Teams.Single(t => t.Id == p.TeamId).Name} · {p.Number} {p.Name}").ToList();
        roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == selectedId);
        SemanticProperties.SetDescription(roster, "Selected player, alternative to clicking the pitch");
        roster.SelectedIndexChanged += (_, _) =>
        {
            if (roster.SelectedIndex < 0) return;
            selectedId = match.Players[roster.SelectedIndex].Id;
            Render();
        };
        casual.Clicked += (_, _) => SetMode(ExplanationMode.Casual);
        analyst.Clicked += (_, _) => SetMode(ExplanationMode.Analyst);
        var segments = new Grid
        {
            ColumnDefinitions = Columns(GridLength.Star, GridLength.Star),
            ColumnSpacing = 3,
            AutomationId = "mode"
        };
        segments.Add(casual, 0);
        segments.Add(analyst, 1);
        SemanticProperties.SetDescription(timeline, "Replay time in seconds, zero to seventy-two");
        timeline.ValueChanged += (_, e) =>
        {
            if (updatingTimeline) return;
            playback.Seek(e.NewValue);
            Render();
            if (!playback.IsPlaying) timer?.Stop();
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
        var back = new Button { Text = "−5 s", AutomationId = "seek-back" };
        back.Clicked += (_, _) => SeekBy(-5);
        var forward = new Button { Text = "+5 s", AutomationId = "seek-forward" };
        forward.Clicked += (_, _) => SeekBy(5);
        var transport = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            Children = { restart, back, play, forward }
        };
        var replay = new Grid
        {
            RowDefinitions = Rows(GridLength.Auto, GridLength.Star, GridLength.Auto),
            RowSpacing = 12
        };
        var pitchHeader = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto) };
        pitchHeader.Add(Heading("Match replay"));
        pitchHeader.Add(possession, 1);
        replay.Add(pitchHeader, 0, 0);
        replay.Add(pitch, 0, 1);
        var footer = new VerticalStackLayout { Spacing = 6 };
        var timeRow = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto) };
        timeRow.Add(Caption("72-second synthetic match"));
        timeRow.Add(clock, 1);
        footer.Children.Add(timeRow);
        footer.Children.Add(timeline);
        footer.Children.Add(transport);
        footer.Children.Add(legend);
        replay.Add(footer, 0, 2);
        replayPanel = Panel(replay);

        avatar = new Border
        {
            Content = shirt,
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Color.FromArgb("#007AFF")
        };
        var person = new HorizontalStackLayout
        {
            Spacing = 12,
            Children = { avatar, new VerticalStackLayout { Spacing = 2, Children = { identity, teamIdentity } } }
        };
        var metrics = new Grid
        {
            ColumnDefinitions = Columns(GridLength.Star, GridLength.Star),
            RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto),
            ColumnSpacing = 8,
            RowSpacing = 8
        };
        metrics.Add(Metric("Passes completed", passes), 0, 0);
        metrics.Add(Metric("Pass accuracy", accuracy), 1, 0);
        metrics.Add(Metric("Shots", shots), 0, 1);
        metrics.Add(Metric("Goals", goals), 1, 1);
        metrics.Add(Metric("Distance", distance), 0, 2);
        metrics.Add(Metric("Max speed", speed), 1, 2);
        inspectorPanel = Panel(new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children = { Heading("Player inspector"), roster, person, metrics,
                    Caption("Statistics at the playhead. Distance and speed are trajectory-derived.") }
            }
        });
        var eventGrid = new Grid
        {
            RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Star),
            RowSpacing = 10
        };
        eventsPanel = Panel(eventGrid);
        eventGrid.Add(Heading("Match notes"), 0, 0);
        eventGrid.Add(segments, 0, 1);
        eventGrid.Add(Caption("Scripted event facts · Not generated AI"), 0, 2);
        eventGrid.Add(new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { emptyInsights, insights }
            }
        }, 0, 3);
        sidebar = new Grid { RowDefinitions = Rows(new GridLength(1.2, GridUnitType.Star), GridLength.Star), RowSpacing = 12 };
        sidebar.Add(inspectorPanel, 0, 0);
        sidebar.Add(eventsPanel, 0, 1);
        main.Children.Add(replayPanel);
        main.Children.Add(sidebar);
        var header = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 16 };
        header.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children = { new Label { Text = "PitchLens", FontSize = 19, FontAttributes = FontAttributes.Bold },
                Caption("Synthetic match · Local demo") }
        });
        header.Add(new VerticalStackLayout
        {
            Spacing = 3,
            HorizontalOptions = LayoutOptions.End,
            Children = { scoreboard, teamStats }
        }, 1);
        teamStats.MaximumWidthRequest = 420;
        var root = new Grid
        {
            Padding = new Thickness(20, 16),
            RowSpacing = 16,
            RowDefinitions = Rows(GridLength.Auto, GridLength.Star)
        };
        root.Add(header, 0, 0);
        root.Add(main, 0, 1);
        Content = root;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SetMode(ExplanationMode.Casual);
    }

    private static ColumnDefinitionCollection Columns(params GridLength[] lengths)
    {
        var result = new ColumnDefinitionCollection();
        foreach (var length in lengths) result.Add(new ColumnDefinition(length));
        return result;
    }

    private static RowDefinitionCollection Rows(params GridLength[] lengths)
    {
        var result = new RowDefinitionCollection();
        foreach (var length in lengths) result.Add(new RowDefinition(length));
        return result;
    }

    private static Label Caption(string text)
    {
        var label = new Label { Text = text, FontSize = 11, LineHeight = 1.15 };
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#68686E"), Color.FromArgb("#AEAEB5"));
        return label;
    }

    private static Label Heading(string text) => new() { Text = text, FontSize = 14, FontAttributes = FontAttributes.Bold };
    private static Label Value() => new() { FontSize = 19, FontAttributes = FontAttributes.Bold };

    private static Border Panel(View content)
    {
        var panel = new Border
        {
            Content = content,
            Padding = 16,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 14 }
        };
        panel.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#27272A"));
        panel.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E4E4E8"), Color.FromArgb("#3A3A3E"));
        return panel;
    }

    private static Border Metric(string title, Label value)
    {
        var tile = new Border
        {
            Padding = new Thickness(10, 9),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new VerticalStackLayout { Spacing = 4, Children = { Caption(title), value } }
        };
        tile.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F5F7"), Color.FromArgb("#323235"));
        return tile;
    }

    private void SetMode(ExplanationMode mode)
    {
        explanationMode = mode;
        foreach (var (button, selected) in new[] { (casual, mode == ExplanationMode.Casual), (analyst, mode == ExplanationMode.Analyst) })
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty,
                Color.FromArgb(selected ? "#E5F0FF" : "#F0F0F3"), Color.FromArgb(selected ? "#244266" : "#38383C"));
            button.SetAppThemeColor(Button.TextColorProperty,
                Color.FromArgb(selected ? "#005BC4" : "#68686E"), Color.FromArgb(selected ? "#85B9FF" : "#AEAEB5"));
            button.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            SemanticProperties.SetDescription(button, $"{button.Text} explanations{(selected ? ", selected" : "")}");
        }
        Render();
    }

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
        var wide = Width >= 920;
        if (wideLayout == wide) return;
        wideLayout = wide;
        main.ColumnDefinitions = wide ? Columns(GridLength.Star, new GridLength(310)) : Columns(GridLength.Star);
        main.RowDefinitions = wide ? Rows(GridLength.Star) : Rows(new GridLength(1.25, GridUnitType.Star), GridLength.Star);
        Grid.SetColumn(sidebar, wide ? 1 : 0);
        Grid.SetRow(sidebar, wide ? 0 : 1);
        sidebar.ColumnDefinitions = wide ? Columns(GridLength.Star) : Columns(GridLength.Star, GridLength.Star);
        sidebar.RowDefinitions = wide ? Rows(new GridLength(1.2, GridUnitType.Star), GridLength.Star) : Rows(GridLength.Star);
        sidebar.ColumnSpacing = wide ? 0 : 12;
        Grid.SetColumn(eventsPanel, wide ? 0 : 1);
        Grid.SetRow(eventsPanel, wide ? 1 : 0);
    }

    private void OnPitchInteraction(object? sender, TouchEventArgs e)
    {
        if (pitch.Width <= 0 || pitch.Height <= 0 || e.Touches.Length == 0) return;
        var point = e.Touches[0];
        var time = drawable.Time;
        var id = PitchTransform.Fit(pitch.Width, pitch.Height).HitTest(
            match.Players.Select(p => (p.Id, p.Path.At(time))),
            new Position(point.X, point.Y), PitchDrawable.PlayerRadius);
        if (id is not null) roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == id);
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
        scoreboard.Text = $"Harbor   {snapshot.Score["harbor"]} – {snapshot.Score["orchard"]}   Orchard";
        teamStats.Text = string.Join("   ·   ", match.Teams.Select(team =>
        {
            var players = snapshot.Players.Where(p => match.Player(p.PlayerId).TeamId == team.Id).ToArray();
            var attempted = players.Sum(p => p.Passes);
            var completed = players.Sum(p => p.CompletedPasses);
            var percent = attempted == 0 ? "—" : (completed / (double)attempted).ToString("P0");
            return $"{team.Name}: {completed}/{attempted} passes ({percent}), {players.Sum(p => p.Shots)} shots";
        }));
        possession.Text = snapshot.OwnerId is null ? "Ball in transit" : $"Possession · {match.Player(snapshot.OwnerId).Name}";
        clock.Text = $"{time:00.0} / {match.Duration:00.0} s" + (time >= match.Duration ? " · Full time" : "");
        play.Text = playback.IsPlaying ? "Pause" : "Play";
        play.IsEnabled = time < match.Duration;
        updatingTimeline = true;
        timeline.Value = time;
        updatingTimeline = false;
        identity.Text = selected.Name;
        teamIdentity.Text = match.Teams.Single(t => t.Id == selected.TeamId).Name;
        shirt.Text = selected.Number.ToString();
        avatar.BackgroundColor = Color.FromArgb(selected.TeamId == "harbor" ? "#2676D9" : "#C66B35");
        passes.Text = $"{s.CompletedPasses} / {s.Passes}";
        accuracy.Text = s.PassAccuracy is double value ? value.ToString("P0", CultureInfo.CurrentCulture) : "Unavailable";
        accuracy.FontSize = s.PassAccuracy.HasValue ? 19 : 14;
        SemanticProperties.SetDescription(accuracy, s.PassAccuracy.HasValue ? $"Pass accuracy {accuracy.Text}" : "Pass accuracy unavailable, no attempts");
        shots.Text = s.Shots.ToString();
        goals.Text = s.Goals.ToString();
        distance.Text = $"{s.Distance:F1} m";
        speed.Text = $"{s.MaxSpeed:F2} m/s";
        SemanticProperties.SetDescription(pitch,
            $"Synthetic replay at {time:F1} seconds. Score Harbor {snapshot.Score["harbor"]}, Orchard {snapshot.Score["orchard"]}. " +
            $"Selected player {selected.Name}, {teamIdentity.Text}, number {selected.Number}. {possession.Text}. Use the player picker to select.");
        var explanations = Explanations.At(match, time, explanationMode);
        var key = $"{explanationMode}:{string.Join(',', explanations.Select(x => x.EventId))}";
        if (key == insightKey) return;
        insightKey = key;
        insights.Children.Clear();
        emptyInsights.IsVisible = explanations.Count == 0;
        foreach (var explanation in explanations.Reverse())
        {
            var seek = new Button
            {
                Text = $"{explanation.Time:00.0} s  ·  {explanation.EventId}  ↗",
                AutomationId = $"event-{explanation.EventId}",
                HorizontalOptions = LayoutOptions.Start,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#007AFF"),
                Padding = 0
            };
            SemanticProperties.SetDescription(seek, $"Replay event {explanation.EventId} at {explanation.Time} seconds");
            seek.Clicked += (_, _) => { Suspend(); playback.Seek(explanation.Time); Render(); };
            var separator = new BoxView { HeightRequest = 1 };
            separator.SetAppThemeColor(BoxView.ColorProperty, Color.FromArgb("#E8E8ED"), Color.FromArgb("#3A3A3E"));
            insights.Children.Add(new VerticalStackLayout
            {
                Spacing = 5,
                Children = { seek, new Label { Text = explanation.Text, FontSize = 12, LineHeight = 1.2 }, separator }
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
