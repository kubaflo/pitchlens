using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class AuthoredMatchTests
{
    [Fact]
    public void All_twenty_two_paths_and_events_are_exact_AI_data_not_a_choreography_expansion()
    {
        var document = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        var match = AuthoredMatchRecording.Create();
        Assert.Equal(22, match.Players.Count);
        Assert.All(match.Teams, team => Assert.Equal(11, match.Players.Count(p => p.TeamId == team.Id)));
        Assert.Equal(document.Events.Select(e => (e.Id, e.Time, e.ActorId)), match.Events.Select(e => (e.Id, e.Time, e.ActorId)));
        foreach (var raw in document.Players)
        {
            var player = match.Player(raw.Id);
            Assert.Equal(raw.Frames.Length, player.Path.Frames.Count);
            Assert.Equal(raw.Frames.Select(f => new Keyframe(f[0], new(f[1], f[2]))), player.Path.Frames);
            for (var i = 1; i < raw.Frames.Length; i++)
            {
                var a = player.Path.Frames[i - 1]; var b = player.Path.Frames[i];
                Assert.Equal(Position.Lerp(a.Position, b.Position, .5), player.Path.At((a.Time + b.Time) / 2));
            }
        }
        Assert.Equal(300, match.Duration);
        Assert.Equal(805, match.Players.Sum(p => p.Path.Frames.Count));
        Assert.Equal(90, match.Events.Count);
        Assert.Equal(63, match.Events.Count(e => e.Kind == EventKind.Pass));
        Assert.Equal(21, match.Events.Count(e => e.Kind == EventKind.Carry));
        Assert.Equal(2, match.Events.Count(e => e.Kind == EventKind.Goal));
        Assert.Equal(41, match.Commentary!.Cues.Count);
        AuthoredPathQuality.Validate(match);
    }

    [Fact]
    public void Default_three_x_replay_selection_lifecycle_and_ball_binding_cover_the_whole_match()
    {
        var match = AuthoredMatchRecording.Create();
        var clock = new Clock();
        using var surface = new ReplayPresentation(match, clock);
        Assert.Equal(3, surface.Rate);
        surface.Select("arsenal-martinelli");
        surface.Play();
        clock.Seconds = 10;
        Assert.Equal(30, surface.Position);
        for (var step = 0; step <= 3000; step++)
        {
            var time = step / 10d;
            if (match.OwnerAt(time) is { } owner)
                Assert.True(match.Player(owner).Path.At(time).DistanceTo(match.Ball.At(time)) < 1e-8);
        }
        clock.Seconds = 200;
        Assert.Equal(300, surface.Sample(true)!.Snapshot.Time);
        surface.Toggle();
        Assert.Equal(0, surface.Position);
        Assert.Equal(3, surface.Rate);
        Assert.Equal("arsenal-martinelli", surface.SelectedId);
        surface.Seek(0);
        Assert.Empty(surface.Sample(true)!.Explanations);
        surface.Replace(match);
        Assert.Equal(3, surface.Rate);
        Assert.Null(CommentaryReplay.At(surface.Match.Commentary, 0));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("teleport")]
    [InlineData("keeper")]
    [InlineData("malformed-knot")]
    public void Invalid_AI_paths_are_rejected_not_repaired(string problem)
    {
        var original = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        var players = original.Players.Select(p => p with { Frames = p.Frames.Select(f => f.ToArray()).ToArray() }).ToArray();
        if (problem == "missing") players = players[..21];
        else if (problem == "unknown") players[0] = players[0] with { Id = "invented" };
        else if (problem == "duplicate") players[1] = players[0];
        else if (problem == "teleport") players[1].Frames[1] = [players[1].Frames[0][0] + .1, 100, 65];
        else if (problem == "keeper") players[0] = players[0] with { Frames = players[0].Frames.Select(f => new[] { f[0], 95d, f[2] }).ToArray() };
        else players[1].Frames[1] = [1, 2];
        Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(original with { Players = players }, requireCommentary: false));
        Assert.Equal(22, original.Players.Length);
    }

    [Fact]
    public void Fresh_generation_contract_requires_22_AI_paths_and_its_own_captions()
    {
        var match = AuthoredMatchRecording.Create();
        var document = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(document));
        var complete = document with { Commentary = match.Commentary!.Cues.ToArray() };
        var roundtrip = AuthoredMatchPlan.Parse(JsonSerializer.Serialize(complete, AuthoredJsonContext.Default.AuthoredMatchDocument));
        Assert.Equal(match.Players.Sum(p => p.Path.Frames.Count), roundtrip.Players.Sum(p => p.Path.Frames.Count));
        Assert.Equal(match.Commentary.Cues.Count, roundtrip.Commentary!.Cues.Count);
        Assert.Equal("gpt-6.1-sol", roundtrip.Commentary.Model);
    }

    [Fact]
    public void Factual_roster_has_11_per_team_explicit_keeper_IDs_and_expanded_names()
    {
        Assert.Equal(22, RegulationRoster.Players.Select(p => p.Id).Distinct().Count());
        Assert.All(RegulationRoster.Teams, t => Assert.Equal(11, RegulationRoster.Players.Count(p => p.TeamId == t.Id)));
        Assert.Equal(2, RegulationRoster.Players.Count(p => RegulationRoster.IsKeeper(p.Id)));
        Assert.Equal(82, RegulationRoster.Players.Single(p => p.Id == "manchester-city-lewis").Number);
        Assert.Equal("Gabriel Martinelli", RegulationRoster.Players.Single(p => p.Id == "arsenal-martinelli").Name);
    }

    [Fact]
    public void New_goal_caption_story_and_score_are_exact_and_do_not_spoil_after_backward_seeks()
    {
        var match = AuthoredMatchRecording.Create();
        Assert.Equal(1, Replay.At(match, 300).Score["arsenal"]);
        Assert.Equal(1, Replay.At(match, 300).Score["manchester-city"]);
        foreach (var goal in match.Events.Where(e => e.Kind == EventKind.Goal))
        {
            Assert.Equal(goal.Id, CommentaryReplay.At(match.Commentary, goal.Time)!.EventId);
            Assert.Equal(goal.Id, GoalReplay.At(match, goal.Time)!.EventId);
            Assert.NotEqual(goal.Id, CommentaryReplay.At(match.Commentary, goal.Time - .001)?.EventId);
        }
        foreach (var time in new[] { 300d, 175, 85, 0, 176, 86 })
        {
            Assert.All(Replay.At(match, time).Events, e => Assert.True(e.Time <= time));
            Assert.All(StoryReplay.At(match, time), e => Assert.True(e.Time <= time));
        }
        Assert.Contains(match.Commentary!.Cues, c => c.Text.Contains("Gabriel Martinelli") && c.PlayerIds.Contains("arsenal-martinelli") &&
            !c.PlayerIds.Contains("arsenal-gabriel"));
    }

    [Fact]
    public void Final_authored_paths_have_individual_running_bursts_and_wide_noncollapsing_team_shapes()
    {
        var match = AuthoredMatchRecording.Create();
        foreach (var team in match.Teams)
        {
            var outfield = match.Players.Where(p => p.TeamId == team.Id && !RegulationRoster.IsKeeper(p.Id)).ToArray();
            Assert.True(outfield.Count(p => p.Path.Measure(300).MaxSpeed >= 3) >= 4);
            Assert.True(outfield.Count(p => p.Path.Measure(300).MaxSpeed >= 4) >= 2);
            for (double time = 0; time <= 300; time++)
            {
                var shape = outfield.Select(p => p.Path.At(time)).ToArray();
                Assert.True(shape.Max(p => p.Y) - shape.Min(p => p.Y) >= 30);
                Assert.True(shape.Max(p => p.X) - shape.Min(p => p.X) >= 22);
            }
        }
    }
    [Fact]
    public void Obvious_offside_receiver_is_rejected_without_auto_repositioning()
    {
        var document = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        var pass = document.Events.First(e => e.Kind == "pass" && e.Completed == true && e.ActorId.StartsWith("arsenal-"));
        var players = document.Players.Select(p => p.Id == pass.ReceiverId
            ? p with { Frames = p.Frames.Select(f => new[] { f[0], 104d, f[2] }).ToArray() } : p).ToArray();
        var error = Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(document with { Players = players }, requireCommentary: false));
        Assert.Contains("offside", error.Message);
        Assert.All(players.Single(p => p.Id == pass.ReceiverId).Frames, f => Assert.Equal(104, f[1]));
    }

    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
}
