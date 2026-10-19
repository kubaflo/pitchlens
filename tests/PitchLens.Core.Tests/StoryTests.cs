using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class StoryTests
{
    private static ScenarioDocument Document() => ScenarioParser.ParseDocument(RecordedScenario.Json());

    [Fact]
    public void Punchier_moment_titles_name_only_resolved_goal_or_interception_evidence()
    {
        var match = AuthoredMatchRecording.Create();
        foreach (var beat in match.Story!.Beats.Where(b => b.Kind is "finish" or "turnover"))
        {
            Assert.DoesNotContain(StoryReplay.At(match, beat.EndTime - .001), m => m.Id == beat.Id);
            var moment = StoryReplay.At(match, beat.EndTime).Single(m => m.Id == beat.Id);
            var evidence = beat.EventIds.Select(id => match.Events.Single(e => e.Id == id)).ToArray();
            var player = beat.Kind == "finish" ? evidence.First(e => e.Kind == EventKind.Goal).ActorId
                : evidence.First(e => e.Kind == EventKind.Pass && e.Completed == false).ReceiverId!;
            Assert.Contains(match.Player(player).Name, moment.Heading);
        }
    }

    [Fact]
    public void Recorded_example_has_complete_causal_story_and_both_teams_participate()
    {
        var match = RecordedScenario.Create();
        var story = Assert.IsType<MatchStory>(match.Story);
        Assert.Equal(0, story.Beats[0].StartTime);
        Assert.Equal(72, story.Beats[^1].EndTime);
        foreach (var kind in new[] { "build-up", "turnover", "counterattack", "support-run", "finish", "response" })
            Assert.Contains(story.Beats, b => b.Kind == kind);
        foreach (var team in match.Teams)
        {
            Assert.Contains(match.Events, e => e.Kind == EventKind.Pass && e.Completed == true && match.Player(e.ActorId).TeamId == team.Id);
            Assert.Contains(match.Events, e => e.Kind == EventKind.Carry && match.Player(e.ActorId).TeamId == team.Id);
            Assert.True(match.Players.Count(p => p.TeamId == team.Id && StoryQuality.IsActiveOutfielder(p)) >= 3);
        }
        Assert.Contains(match.Events, e => e.Kind == EventKind.Pass && e.Completed == false);
        var snapshot = Replay.At(match, 72);
        Assert.Equal(20, match.Events.Count);
        Assert.Equal(9, snapshot.Players.Sum(p => p.Passes));
        Assert.Equal(8, match.Events.Count(e => e.Kind == EventKind.Carry));
        Assert.Equal(1, snapshot.Players.Sum(p => p.Goals));
        Assert.Equal(1, snapshot.Score["manchester-city"]);
    }

    [Fact]
    public void Story_progress_is_seek_safe_and_never_reveals_future_event_or_movement_claims()
    {
        var match = RecordedScenario.Create();
        foreach (var time in new[] { 0d, 8, 18, 35, 52, 72, 0, 30, 72 })
        {
            var moments = StoryReplay.At(match, time);
            Assert.All(moments, moment =>
            {
                Assert.True(moment.Time <= time);
                Assert.All(moment.EventIds, id => Assert.True(match.Events.Single(e => e.Id == id).Time <= time));
            });
            Assert.Equal(match.Story!.Beats.Count(b => b.EndTime <= time), moments.Count);
        }
        Assert.Empty(StoryReplay.At(match, 0));
        Assert.Equal(StoryReplay.At(match, 72), StoryReplay.At(match, 72));
    }

    [Fact]
    public void Recorded_provenance_does_not_claim_a_fresh_generation()
    {
        var service = new GenerationService(new NeverGenerate(), RecordedScenario.Create(), ScenarioOrigin.RecordedSdkExample);
        Assert.Equal(ScenarioOrigin.RecordedSdkExample, service.Origin);
        Assert.False(service.IsSdkGenerated);
        Assert.Contains("Recorded SDK", service.Status);
        Assert.Equal(GenerationState.Ready, service.State);
    }

    [Theory]
    [InlineData("unknown-reference")]
    [InlineData("gap")]
    [InlineData("future-reference")]
    [InlineData("missing-arc")]
    public void Invalid_story_anchors_coverage_or_arc_are_rejected(string change)
    {
        var doc = Document();
        var beats = doc.Story.Beats.ToArray();
        beats[0] = change switch
        {
            "unknown-reference" => beats[0] with { EventIds = ["nonexistent"] },
            "gap" => beats[0] with { StartTime = 1 },
            "future-reference" => beats[0] with { EventIds = [doc.Events.Last().Id] },
            _ => beats[0] with { Kind = "finish" }
        };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(doc with { Story = doc.Story with { Beats = beats } }));
    }

    [Fact]
    public void Opposing_keeper_at_wrong_end_is_rejected()
    {
        var doc = Document();
        var players = doc.Players.Select(p => p.Id == "manchester-city-donnarumma"
            ? p with { Frames = p.Frames.Select(f => f with { X = 5 }).ToArray() } : p).ToArray();
        Assert.Contains("Keepers", Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(doc with { Players = players })).Message);
    }

    [Fact]
    public void Stationary_background_paths_are_not_accepted_as_an_engaging_match()
    {
        var match = RecordedScenario.Create();
        var players = match.Players.Select(p => StoryQuality.IsKeeper(p) ? p
            : p with { Path = new Trajectory(p.Path.Frames.Select(f => f with { Position = p.Path.At(0) })) }).ToArray();
        var flat = new Match(72, match.Teams, players, match.Events, match.Ball, match.Possession);
        Assert.Throws<ScenarioValidationException>(() => StoryQuality.Validate(flat, Document().Story));
        Assert.All(players.Where(p => !StoryQuality.IsKeeper(p)), p => Assert.False(StoryQuality.IsActiveOutfielder(p)));
    }

    [Fact]
    public void Direction_changes_ignore_tiny_jitter_but_count_purposeful_turns()
    {
        var jitter = new Trajectory([new(0, new(10, 10)), new(1, new(10.1, 10)), new(2, new(10, 10.1)), new(3, new(10.1, 10))]);
        var runs = new Trajectory([new(0, new(10, 10)), new(4, new(30, 10)), new(8, new(30, 30)), new(12, new(10, 30))]);
        Assert.Equal(0, StoryQuality.DirectionChanges(jitter));
        Assert.Equal(2, StoryQuality.DirectionChanges(runs));
    }

    [Fact]
    public void Pressure_claim_needs_a_named_defender_reacting_to_the_ball()
    {
        var doc = Document();
        var beats = doc.Story.Beats.ToArray();
        var attackTeam = PremierLeagueRoster.Players.Single(p => p.Id ==
            doc.Events.Single(e => e.Id == beats[0].EventIds[0]).ActorId).TeamId;
        beats[0] = beats[0] with
        {
            PlayerIds = beats[0].PlayerIds.Where(id =>
            PremierLeagueRoster.Players.Single(p => p.Id == id).TeamId == attackTeam).ToArray()
        };
        var error = Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(doc with
        { Story = doc.Story with { Beats = beats } }));
        Assert.Contains("opposing defender", error.Message);
    }

    [Fact]
    public void Support_run_claim_is_rejected_when_receiver_does_not_advance()
    {
        var doc = Document();
        var beat = doc.Story.Beats.First(b => b.Kind == "support-run");
        var pass = doc.Events.First(e => beat.EventIds.Contains(e.Id) && e.Kind == "pass" && e.Completed == true);
        var match = RecordedScenario.Create();
        var from = match.Player(pass.ReceiverId!).Path.At(beat.StartTime);
        var players = doc.Players.Select(p => p.Id != pass.ReceiverId ? p :
            p with { Frames = p.Frames.Select(f => f.Time == pass.Time ? f with { X = from.X, Y = from.Y } : f).ToArray() }).ToArray();
        var error = Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(doc with { Players = players }));
        Assert.Contains("Support beat", error.Message);
    }

    private sealed class NeverGenerate : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Startup must not invoke a paid request.");
    }
}
