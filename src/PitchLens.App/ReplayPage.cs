using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.App;

public sealed class ReplayPage : ContentPage, IDisposable
{
    private Match match = RecordedScenario.Create();
    private Playback playback = new(72, new MonotonicClock());
    private readonly GenerationService generation;
    private readonly Button generate = new() { Text = "Generate match", AutomationId = "generate-match" };
    private readonly Button cancelGeneration = new() { Text = "Cancel", AutomationId = "cancel-generation", IsVisible = false };
    private readonly ActivityIndicator generating = new() { Color = MatchTheme.Purple, IsVisible = false };
    private readonly Label generationStatus = Caption("Recorded SDK example · Not a fresh request");
    private readonly Label storyTitle = Heading("", 17);
    private readonly Label storyBeat = Caption("Opening shape · Play to reveal the story");
    private readonly VerticalStackLayout storyNotes = new() { Spacing = 10 };
    private readonly Entry cliPath = new()
    {
        Placeholder = "Bundled runtime (leave blank)",
        FontSize = 12,
        AutomationId = "copilot-cli-path",
        MinimumWidthRequest = 180
    };
    private readonly Label dataset = Caption("");
    private CancellationTokenSource? generationCancellation;
    private readonly PitchDrawable drawable;
    private readonly GraphicsView pitch;
    private readonly Label scoreboard = new()
    {
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        VerticalTextAlignment = TextAlignment.Center
    };
    private readonly Label clock = Caption("");
    private readonly Label possession = Caption("");
    private readonly Label identity = Heading("", 24);
    private readonly Label teamIdentity = Caption("");
    private readonly Image selectedClubCrest = CrestImage("arsenal", 28);
    private readonly Dictionary<string, Label> stats = [];
    private readonly Label emptyInsights = Caption("No events yet. Play or seek to reveal the match.");
    private readonly Button play = new()
    {
        Text = "Play",
        AutomationId = "play-pause",
        WidthRequest = 80,
        BackgroundColor = MatchTheme.Mint,
        TextColor = MatchTheme.Purple
    };
    private readonly Slider timeline = new() { Minimum = 0, Maximum = DemoMatch.Duration, AutomationId = "timeline" };
    private readonly Picker roster = new() { Title = "Select player", AutomationId = "roster" };
    private readonly Button casual = new() { Text = "Casual", AutomationId = "mode-casual" };
    private readonly Button analyst = new() { Text = "Analyst", AutomationId = "mode-analyst" };
    private readonly VerticalStackLayout insights = new() { Spacing = 14 };
    private readonly Grid main = new() { ColumnSpacing = 16, RowSpacing = 12 };
    private readonly Grid sidebar;
    private readonly Border eventsPanel;
    private readonly Border inspectorPanel;
    private readonly Button playerLens = new() { Text = "Player lens", AutomationId = "inspector-player" };
    private readonly Button notes = new() { Text = "Match notes", AutomationId = "inspector-notes" };
    private readonly Border replayPanel;
    private readonly ScrollView dashboard;
    private readonly VerticalStackLayout playerSection = new() { Spacing = 12 };
    private readonly VerticalStackLayout clubSection = new() { Spacing = 12 };
    private readonly List<PlayerBoard> playerBoards = [];
    private readonly List<ClubBoard> clubBoards = [];
    private readonly Dictionary<string, Button> tabs = [];
    private readonly Dictionary<string, BoxView> tabLines = [];
    private IDispatcherTimer? timer;
    private string selectedId = "arsenal-odegaard";
    private ExplanationMode explanationMode;
    private bool updatingTimeline, disposed;
    private string insightKey = "";
    private bool? wideLayout;

    public ReplayPage()
    {
        generation = new GenerationService(new AppScenarioSource(this), match, ScenarioOrigin.RecordedSdkExample);
        dataset.AutomationId = "scenario-provenance";
        generationStatus.AutomationId = "generation-status";
        Title = "PitchLens · Match Centre";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#FAF9FA"), Color.FromArgb("#1E0021"));
        drawable = new(match) { SelectedId = selectedId };
        pitch = new GraphicsView { Drawable = drawable, AutomationId = "pitch", MinimumHeightRequest = 60 };
        SemanticProperties.SetDescription(pitch, "Synthetic football replay. Use the player picker for accessible selection.");
        pitch.StartInteraction += OnPitchInteraction;
        roster.ItemsSource = match.Players.Select(p => $"{TeamName(p.TeamId)} · {p.Number} {p.Name}").ToList();
        roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == selectedId);
        SemanticProperties.SetDescription(roster, "Selected player, alternative to clicking the pitch or a leaderboard row");
        roster.SelectedIndexChanged += (_, _) =>
        {
            if (roster.SelectedIndex < 0) return;
            selectedId = match.Players[roster.SelectedIndex].Id;
            Render();
        };
        casual.Clicked += (_, _) => SetMode(ExplanationMode.Casual);
        analyst.Clicked += (_, _) => SetMode(ExplanationMode.Analyst);
        var segments = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Star), ColumnSpacing = 3, AutomationId = "mode" };
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
        var controls = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            Children = { restart, back, play, forward }
        };
        var timeRow = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 12 };
        timeRow.Add(timeline);
        timeRow.Add(clock, 1);
        var transport = Panel(new VerticalStackLayout { Spacing = 2, Children = { timeRow, controls } }, 10);

        var replayHeader = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 8 };
        replayHeader.Add(Heading("Match replay", 22));
        replayHeader.Add(possession, 1);
        var replay = new Grid { RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Star, GridLength.Auto), RowSpacing = 8 };
        replay.Add(replayHeader);
        storyTitle.AutomationId = "story-title";
        storyBeat.AutomationId = "current-story-beat";
        replay.Add(new VerticalStackLayout { Spacing = 2, Children = { storyTitle, storyBeat } }, 0, 1);
        replay.Add(pitch, 0, 2);
        replay.Add(Caption("Mint: Arsenal · Pink: Manchester City · White: ball & selection · Lilac: past movement"), 0, 3);
        replayPanel = Panel(replay);

        playerSection.Children.Add(Heading("Player stats", 24));
        playerSection.Children.Add(Caption("Top 5 synthetic players · Select a row to inspect · Ties ordered by player ID"));
        var playerCards = new HorizontalStackLayout { Spacing = 16 };
        AddPlayerBoard(playerCards, "Goals", s => s.Goals, n => n.ToString("0"));
        AddPlayerBoard(playerCards, "Shots", s => s.Shots, n => n.ToString("0"));
        AddPlayerBoard(playerCards, "Passes", s => s.Passes, n => n.ToString("0"));
        AddPlayerBoard(playerCards, "Distance", s => s.Distance, n => $"{n:F1} m");
        playerSection.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = playerCards });
        clubSection.Children.Add(Heading("Club stats", 24));
        clubSection.Children.Add(Caption("2026/27 roster snapshot · Synthetic match, not real club performance"));
        var clubCards = new HorizontalStackLayout { Spacing = 16 };
        AddClubBoard(clubCards, "Goals", s => s.Goals, n => n.ToString("0"));
        AddClubBoard(clubCards, "Shots", s => s.Shots, n => n.ToString("0"));
        AddClubBoard(clubCards, "Passes", s => s.Passes, n => n.ToString("0"));
        AddClubBoard(clubCards, "Distance", s => s.Distance, n => $"{n:F1} m");
        clubSection.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = clubCards });
        dashboard = new ScrollView { Content = new VerticalStackLayout { Spacing = 24, Children = { playerSection, clubSection } } };

        var statRows = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), RowSpacing = 12, ColumnSpacing = 8 };
        foreach (var title in new[] { "Passes completed", "Pass accuracy", "Shots", "Goals", "Distance", "Max speed" })
        {
            var row = statRows.RowDefinitions.Count;
            statRows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var value = Heading("", 14);
            value.HorizontalTextAlignment = TextAlignment.End;
            statRows.Add(Caption(title), 0, row);
            statRows.Add(value, 1, row);
            stats.Add(title, value);
        }
        var selectedClub = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star), ColumnSpacing = 8 };
        selectedClub.Add(CrestSurface(selectedClubCrest));
        selectedClub.Add(teamIdentity, 1);
        inspectorPanel = Panel(new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children = { Heading("Selected player", 18), roster, identity, selectedClub, statRows,
                    Caption("Resolved synthetic attempts only. Not actual player performance."),
                    Caption("Copilot SDK generation"),
                    Caption($"GPT only: {CopilotScenarioSource.Model}. Uses bundled compatible runtime and normal copilot login. One bounded request per click; may consume Copilot usage."),
                    Caption("Optional compatible CLI override (absolute path):"),
                    cliPath }
            }
        });
        var eventGrid = new Grid { RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Star), RowSpacing = 10 };
        eventsPanel = Panel(eventGrid);
        eventGrid.Add(Heading("Match notes", 18));
        eventGrid.Add(segments, 0, 1);
        eventGrid.Add(Caption("Scripted explanations · No AI commentary"), 0, 2);
        eventGrid.Add(new ScrollView { Content = new VerticalStackLayout { Spacing = 10, Children = { storyNotes, emptyInsights, insights } } }, 0, 3);
        var inspectorTabs = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Star), ColumnSpacing = 4 };
        inspectorTabs.Add(playerLens);
        inspectorTabs.Add(notes, 1);
        playerLens.Clicked += (_, _) => SelectInspector(false);
        notes.Clicked += (_, _) => SelectInspector(true);
        var inspectorBody = new Grid();
        inspectorBody.Children.Add(inspectorPanel);
        inspectorBody.Children.Add(eventsPanel);
        sidebar = new Grid { RowDefinitions = Rows(GridLength.Auto, GridLength.Star), RowSpacing = 8 };
        sidebar.Add(inspectorTabs);
        sidebar.Add(inspectorBody, 0, 1);
        var workspace = new Grid();
        workspace.Children.Add(dashboard);
        workspace.Children.Add(replayPanel);
        main.Children.Add(workspace);
        main.Children.Add(sidebar);

        var nav = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star, GridLength.Auto), ColumnSpacing = 12 };
        nav.Add(Heading("PitchLens", 17));
        nav.Add(Caption("Independent · 2026/27 names · Synthetic actions"), 1);
        var openReplay = new Button { Text = "Match replay", AutomationId = "open-replay", Padding = new Thickness(10, 4), MinimumHeightRequest = 28 };
        openReplay.Clicked += (_, _) => SelectTab("Match replay");
        nav.Add(openReplay, 2);
        var tabBar = new HorizontalStackLayout { Spacing = 20 };
        foreach (var name in new[] { "Match replay", "Dashboard", "Player", "Club" })
        {
            var button = new Button
            {
                Text = name,
                AutomationId = $"tab-{name.ToLowerInvariant().Replace(' ', '-')}",
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.White,
                FontSize = 13,
                Padding = 0,
                MinimumHeightRequest = 32
            };
            var line = new BoxView { HeightRequest = 3, Color = Colors.White, Opacity = 0 };
            button.Clicked += (_, _) => SelectTab(name);
            tabs.Add(name, button);
            tabLines.Add(name, line);
            tabBar.Children.Add(new VerticalStackLayout { Spacing = 2, Children = { button, line } });
        }
        var hero = new Grid
        {
            Padding = new Thickness(20, 10),
            RowDefinitions = Rows(GridLength.Auto, GridLength.Auto),
            RowSpacing = 4,
            Background = new LinearGradientBrush(
                new GradientStopCollection { new GradientStop(Color.FromArgb("#953BFF"), 0), new GradientStop(Color.FromArgb("#05F0FF"), 1) },
                new Point(0, .5), new Point(1, .5))
        };
        hero.Add(new Label { Text = "Match Centre", FontSize = 25, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        hero.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = tabBar }, 0, 1);
        var matchStrip = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 12 };
        matchStrip.Add(dataset);
        matchStrip.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { CrestSurface(CrestImage("arsenal", 24)), scoreboard,
                CrestSurface(CrestImage("manchester-city", 24)) }
        }, 1);
        generate.Clicked += OnGenerateClicked;
        cancelGeneration.Clicked += (_, _) => generationCancellation?.Cancel();
        var generationRow = new Grid
        {
            ColumnDefinitions = Columns(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Star),
            ColumnSpacing = 8,
            RowDefinitions = Rows(GridLength.Auto)
        };
        generationRow.Add(generate);
        generationRow.Add(cancelGeneration, 1);
        generationRow.Add(generating, 2);
        generationRow.Add(generationStatus, 3);
        var root = new Grid
        {
            Padding = new Thickness(20, 12),
            RowSpacing = 12,
            RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Star, GridLength.Auto, GridLength.Auto)
        };
        root.Add(nav);
        root.Add(hero, 0, 1);
        root.Add(matchStrip, 0, 2);
        root.Add(main, 0, 3);
        root.Add(transport, 0, 4);
        root.Add(generationRow, 0, 5);
        Content = root;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SelectInspector(false);
        SelectTab("Match replay");
        SetMode(ExplanationMode.Casual);
    }

    private string TeamName(string id) => match.Teams.Single(t => t.Id == id).Name;

    private static string CrestSource(string teamId) => teamId switch
    {
        "arsenal" => "arsenal_crest.png",
        "manchester-city" => "manchester_city_crest.png",
        _ => throw new ArgumentException("Unknown crest team.", nameof(teamId))
    };

    private static Image CrestImage(string teamId, double size)
    {
        var image = new Image
        {
            Source = CrestSource(teamId),
            Aspect = Aspect.AspectFit,
            WidthRequest = size,
            HeightRequest = size,
            VerticalOptions = LayoutOptions.Center
        };
        SemanticProperties.SetDescription(image, $"{PremierLeagueRoster.Teams.Single(t => t.Id == teamId).Name} club crest");
        return image;
    }

    private static Border CrestSurface(Image image) => new()
    {
        Content = image,
        Padding = 2,
        BackgroundColor = Colors.White,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 5 },
        VerticalOptions = LayoutOptions.Center
    };

    private sealed class AppScenarioSource(ReplayPage page) : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
        {
            var executable = string.IsNullOrWhiteSpace(page.cliPath.Text)
                ? System.IO.Path.Combine(Foundation.NSBundle.MainBundle.ResourcePath
                    ?? throw new CopilotGenerationException("The app resource directory is missing; rebuild the app bundle."),
                    "CopilotRuntime", "prebuilds", "darwin-arm64", "copilot-runtime")
                : page.cliPath.Text.Trim();
            var directory = FileSystem.AppDataDirectory;
            return Task.Run(() => new CopilotScenarioSource(executable, directory).GenerateAsync(progress, cancellationToken), cancellationToken);
        }
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
    {
        if (disposed || generationCancellation is not null) return;
        Suspend();
        var cancellation = new CancellationTokenSource();
        generationCancellation = cancellation;
        generate.IsEnabled = false;
        cancelGeneration.IsVisible = true;
        generating.IsVisible = generating.IsRunning = true;
        cliPath.IsEnabled = false;
        generationStatus.Text = "Connecting to Copilot SDK…";
        var progress = new Progress<string>(message =>
        {
            if (!disposed && generation.State == GenerationState.Generating &&
                ReferenceEquals(generationCancellation, cancellation)) generationStatus.Text = message;
        });
        try
        {
            await generation.GenerateAsync(progress, cancellation.Token);
            if (disposed) return;
            generationStatus.Text = generation.Status;
            if (generation.State == GenerationState.Succeeded)
            {
                match = generation.Current;
                playback.Restart();
                timer?.Stop();
                drawable.Match = match;
                insightKey = "";
                foreach (var board in playerBoards) board.Order = "";
                foreach (var board in clubBoards) board.Order = "";
                SelectTab("Match replay");
                Render();
            }
        }
        finally
        {
            generationCancellation = null;
            cancellation.Dispose();
            if (!disposed)
            {
                generate.IsEnabled = true;
                cancelGeneration.IsVisible = false;
                generating.IsRunning = generating.IsVisible = false;
                cliPath.IsEnabled = true;
            }
        }
    }

    private void SelectTab(string name)
    {
        dashboard.IsVisible = name != "Match replay";
        replayPanel.IsVisible = name == "Match replay";
        playerSection.IsVisible = name is "Dashboard" or "Player";
        clubSection.IsVisible = name is "Dashboard" or "Club";
        foreach (var (label, button) in tabs)
        {
            var selected = label == name;
            tabLines[label].Opacity = selected ? 1 : 0;
            button.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            SemanticProperties.SetDescription(button, $"{label}{(selected ? ", selected" : "")}");
        }
    }

    private void SelectPlayer(string id)
    {
        roster.SelectedIndex = match.Players.ToList().FindIndex(p => p.Id == id);
        SelectInspector(false);
    }

    private void SelectInspector(bool showNotes)
    {
        inspectorPanel.IsVisible = !showNotes;
        eventsPanel.IsVisible = showNotes;
        foreach (var (button, selected) in new[] { (playerLens, !showNotes), (notes, showNotes) })
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty,
                Color.FromArgb(selected ? "#37003C" : "#F5F2F5"), Color.FromArgb(selected ? "#00FF87" : "#46254F"));
            button.SetAppThemeColor(Button.TextColorProperty,
                Color.FromArgb(selected ? "#FFFFFF" : "#735A7D"), Color.FromArgb(selected ? "#37003C" : "#E3CBE9"));
            SemanticProperties.SetDescription(button, $"{button.Text}{(selected ? ", selected" : "")}");
        }
    }

    private void AddPlayerBoard(HorizontalStackLayout cards, string title, Func<PlayerStats, double> metric, Func<double, string> format)
    {
        var list = new VerticalStackLayout { Spacing = 0 };
        var card = Panel(new VerticalStackLayout
        {
            Spacing = 12,
            Children = { Heading(title, 24), list, Caption(title == "Passes" ? "Resolved attempts · Completed totals in inspector" : "Current replay time") }
        });
        card.WidthRequest = 232;
        cards.Children.Add(card);
        playerBoards.Add(new(title, metric, format, list));
    }

    private void AddClubBoard(HorizontalStackLayout cards, string title, Func<PlayerStats, double> metric, Func<double, string> format)
    {
        var list = new VerticalStackLayout { Spacing = 0 };
        var card = Panel(new VerticalStackLayout { Spacing = 12, Children = { Heading(title, 24), list } });
        card.WidthRequest = 232;
        cards.Children.Add(card);
        clubBoards.Add(new(metric, format, list));
    }

    private void UpdateLeaderboards(Snapshot snapshot)
    {
        foreach (var board in playerBoards)
        {
            var ranked = snapshot.Players.OrderByDescending(board.Metric).ThenBy(p => p.PlayerId, StringComparer.Ordinal).Take(5).ToArray();
            var order = string.Join(',', ranked.Select(p => p.PlayerId));
            if (board.Order != order)
            {
                board.Order = order;
                board.List.Children.Clear();
                board.Cells.Clear();
                for (var i = 0; i < ranked.Length; i++)
                {
                    var player = match.Player(ranked[i].PlayerId);
                    var value = Heading("", 19);
                    var row = RankRow(i + 1, player.Name, TeamName(player.TeamId), player.TeamId, value);
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => SelectPlayer(player.Id);
                    row.GestureRecognizers.Add(tap);
                    var choose = new Button
                    {
                        Text = player.Name.Split(' ').Last(),
                        BackgroundColor = Colors.Transparent,
                        HorizontalOptions = LayoutOptions.Start,
                        Padding = 0,
                        FontSize = 13,
                        AutomationId = $"rank-{board.Title.ToLowerInvariant()}-{player.Id}"
                    };
                    choose.Clicked += (_, _) => SelectPlayer(player.Id);
                    // Use a native button for keyboard and accessibility activation, not just the row gesture.
                    var identityCell = (VerticalStackLayout)row.Children[2];
                    identityCell.Children[0] = choose;
                    board.List.Children.Add(row);
                    board.Cells.Add(player.Id, (value, choose));
                }
            }
            foreach (var player in ranked)
            {
                var (value, button) = board.Cells[player.PlayerId];
                value.Text = board.Format(board.Metric(player));
                SemanticProperties.SetDescription(button,
                    $"{match.Player(player.PlayerId).Name}, {TeamName(match.Player(player.PlayerId).TeamId)}, {board.Title} {value.Text}. Select player.");
            }
        }
        foreach (var board in clubBoards)
        {
            var ranked = match.Teams.Select(t => (Team: t,
                Total: snapshot.Players.Where(p => match.Player(p.PlayerId).TeamId == t.Id).Sum(board.Metric)))
                .OrderByDescending(p => p.Total).ThenBy(p => p.Team.Id, StringComparer.Ordinal).ToArray();
            var order = string.Join(',', ranked.Select(p => p.Team.Id));
            if (board.Order != order)
            {
                board.Order = order;
                board.List.Children.Clear();
                board.Values.Clear();
                for (var i = 0; i < ranked.Length; i++)
                {
                    var value = Heading("", 19);
                    board.Values.Add(ranked[i].Team.Id, value);
                    board.List.Children.Add(RankRow(i + 1, ranked[i].Team.Name, "Synthetic match totals", ranked[i].Team.Id, value));
                }
            }
            foreach (var entry in ranked) board.Values[entry.Team.Id].Text = board.Format(entry.Total);
        }
    }

    private static Grid RankRow(int rank, string name, string detail, string teamId, Label total)
    {
        var row = new Grid
        {
            ColumnDefinitions = Columns(new GridLength(16), new GridLength(24), GridLength.Star, GridLength.Auto),
            ColumnSpacing = 7,
            Padding = new Thickness(0, 7),
            MinimumHeightRequest = 55
        };
        var rankLabel = Caption(rank.ToString());
        rankLabel.VerticalOptions = LayoutOptions.Center;
        row.Add(rankLabel);
        row.Add(CrestSurface(CrestImage(teamId, 20)), 1);
        row.Add(new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children = { Heading(name, 13), Caption(detail) }
        }, 2);
        total.VerticalOptions = LayoutOptions.Center;
        total.HorizontalTextAlignment = TextAlignment.End;
        row.Add(total, 3);
        return row;
    }

    private sealed class PlayerBoard(string title, Func<PlayerStats, double> metric, Func<double, string> format, VerticalStackLayout list)
    {
        public string Title { get; } = title;
        public Func<PlayerStats, double> Metric { get; } = metric;
        public Func<double, string> Format { get; } = format;
        public VerticalStackLayout List { get; } = list;
        public string Order { get; set; } = "";
        public Dictionary<string, (Label Value, Button Button)> Cells { get; } = [];
    }

    private sealed class ClubBoard(Func<PlayerStats, double> metric, Func<double, string> format, VerticalStackLayout list)
    {
        public Func<PlayerStats, double> Metric { get; } = metric;
        public Func<double, string> Format { get; } = format;
        public VerticalStackLayout List { get; } = list;
        public string Order { get; set; } = "";
        public Dictionary<string, Label> Values { get; } = [];
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
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#725B78"), Color.FromArgb("#CDB3D1"));
        return label;
    }

    private static Label Heading(string text, double size) => new() { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold };

    private static Border Panel(View content, double padding = 16)
    {
        var panel = new Border
        {
            Content = content,
            Padding = padding,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 }
        };
        panel.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#28002B"));
        panel.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#EBE4EC"), Color.FromArgb("#4B2350"));
        return panel;
    }

    private void SetMode(ExplanationMode mode)
    {
        explanationMode = mode;
        foreach (var (button, selected) in new[] { (casual, mode == ExplanationMode.Casual), (analyst, mode == ExplanationMode.Analyst) })
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty,
                Color.FromArgb(selected ? "#37003C" : "#F5F2F5"), Color.FromArgb(selected ? "#00FF87" : "#46254F"));
            button.SetAppThemeColor(Button.TextColorProperty,
                Color.FromArgb(selected ? "#FFFFFF" : "#735A7D"), Color.FromArgb(selected ? "#37003C" : "#E3CBE9"));
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

    private void OnUnloaded(object? sender, EventArgs e) { Suspend(); generationCancellation?.Cancel(); ReleaseTimer(); }
    private void OnTick(object? sender, EventArgs e) { Render(); if (!playback.IsPlaying) timer?.Stop(); }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        var wide = Width >= 920;
        if (wideLayout == wide) return;
        wideLayout = wide;
        main.ColumnDefinitions = wide ? Columns(GridLength.Star, new GridLength(280)) : Columns(GridLength.Star, new GridLength(245));
        main.RowDefinitions = Rows(GridLength.Star);
        Grid.SetColumn(sidebar, 1);
    }

    private void OnPitchInteraction(object? sender, TouchEventArgs e)
    {
        if (pitch.Width <= 0 || pitch.Height <= 0 || e.Touches.Length == 0) return;
        var point = e.Touches[0];
        var time = drawable.Time;
        var id = PitchTransform.Fit(pitch.Width, pitch.Height).HitTest(
            match.Players.Select(p => (p.Id, p.Path.At(time))),
            new Position(point.X, point.Y), PitchDrawable.PlayerRadius);
        if (id is not null) SelectPlayer(id);
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
        scoreboard.Text = $"Arsenal  {snapshot.Score["arsenal"]} : {snapshot.Score["manchester-city"]}  Man City";
        dataset.Text = $"2026/27 · Synthetic 6v6 · {(generation.IsSdkGenerated ? "Fresh Copilot SDK / GPT" : "Recorded SDK example")}";
        storyTitle.Text = match.Story?.Title ?? "Synthetic match";
        var moments = StoryReplay.At(match, time);
        var currentMoment = moments.LastOrDefault();
        storyBeat.Text = currentMoment is null ? "Opening shape · Play to reveal the story"
            : $"{currentMoment.Time:00.0} s · {currentMoment.Heading}";
        possession.Text = snapshot.OwnerId is null ? "Ball in transit" : $"Possession · {match.Player(snapshot.OwnerId).Name}";
        clock.Text = $"{time:00.0} / {match.Duration:00.0} s" + (time >= match.Duration ? " · Full time" : "");
        play.Text = playback.IsPlaying ? "Pause" : "Play";
        play.IsEnabled = time < match.Duration;
        updatingTimeline = true;
        timeline.Value = time;
        updatingTimeline = false;
        identity.Text = $"{selected.Name}  #{selected.Number}";
        teamIdentity.Text = $"{TeamName(selected.TeamId)} · Real roster name, synthetic actions";
        var crest = CrestSource(selected.TeamId);
        if (selectedClubCrest.Source is not FileImageSource source || source.File != crest)
        {
            selectedClubCrest.Source = crest;
            SemanticProperties.SetDescription(selectedClubCrest, $"{TeamName(selected.TeamId)} club crest");
        }
        var accuracy = s.PassAccuracy is double value ? value.ToString("P0") : "Unavailable (no attempts)";
        stats["Passes completed"].Text = $"{s.CompletedPasses} / {s.Passes}";
        stats["Pass accuracy"].Text = accuracy;
        stats["Pass accuracy"].FontSize = s.PassAccuracy.HasValue ? 14 : 11;
        stats["Shots"].Text = s.Shots.ToString();
        stats["Goals"].Text = s.Goals.ToString();
        stats["Distance"].Text = $"{s.Distance:F1} m";
        stats["Max speed"].Text = $"{s.MaxSpeed:F2} m/s";
        SemanticProperties.SetDescription(pitch,
            $"Synthetic replay at {time:F1} seconds. {scoreboard.Text}. Selected {selected.Name}. {possession.Text}. Use the player picker to select.");
        UpdateLeaderboards(snapshot);
        var explanations = Explanations.At(match, time, explanationMode);
        var key = $"{explanationMode}:{string.Join(',', explanations.Select(x => x.EventId))}:{currentMoment?.Id}";
        if (key == insightKey) return;
        insightKey = key;
        storyNotes.Children.Clear();
        foreach (var moment in moments.Reverse())
            storyNotes.Children.Add(new VerticalStackLayout
            {
                Spacing = 3,
                Children =
            {
                Heading($"{moment.Time:00.0} s · {moment.Heading}", 14),
                new Label { Text = moment.Detail, FontSize = 12 },
                Caption($"Story {moment.Id} · Events {string.Join(", ", moment.EventIds)}")
            }
            });
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
                Padding = 0
            };
            seek.SetAppThemeColor(Button.TextColorProperty, MatchTheme.Purple, MatchTheme.Mint);
            SemanticProperties.SetDescription(seek, $"Replay event {explanation.EventId} at {explanation.Time} seconds");
            seek.Clicked += (_, _) => { Suspend(); playback.Seek(explanation.Time); Render(); };
            insights.Children.Add(new VerticalStackLayout
            {
                Spacing = 5,
                Children = { seek, new Label { Text = explanation.Text, FontSize = 12, LineHeight = 1.2 } }
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
        generationCancellation?.Cancel();
        disposed = true;
        ReleaseTimer();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        pitch.StartInteraction -= OnPitchInteraction;
    }
}
