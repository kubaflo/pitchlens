using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class RateAndCommentaryTests
{
    [Fact]
    public void Rate_changes_reanchor_without_jumps_and_preserve_pause_seek_and_restart()
    {
        var clock = new Clock();
        var playback = new Playback(300, clock);
        Assert.Equal(3, playback.Rate);
        playback.SetRate(1);
        playback.Play();
        clock.Seconds = 2.5;
        playback.SetRate(2);
        Assert.Equal(2.5, playback.Position);
        clock.Seconds++;
        Assert.Equal(4.5, playback.Position);
        playback.SetRate(3);
        clock.Seconds += 2;
        Assert.Equal(10.5, playback.Position);
        playback.Pause();
        playback.SetRate(5);
        clock.Seconds += 100;
        Assert.Equal(10.5, playback.Position);
        playback.Seek(40);
        playback.Play();
        clock.Seconds += 2;
        Assert.Equal(50, playback.Position);
        playback.Seek(playback.Position - 5);
        clock.Seconds++;
        Assert.Equal(50, playback.Position);
        playback.Seek(299);
        clock.Seconds++;
        Assert.Equal(300, playback.Position);
        Assert.False(playback.IsPlaying);
        playback.Restart();
        Assert.Equal(5, playback.Rate);
        Assert.Equal(0, playback.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => playback.SetRate(4));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void Speed_is_exact_across_render_frequencies_and_retained_on_replacement(double rate)
    {
        foreach (var frequency in new[] { 24, 60, 144 })
        {
            var clock = new Clock();
            using var surface = new ReplayPresentation(FiveMinuteRecording.Create(), clock);
            surface.SetRate(rate);
            surface.Play();
            for (int i = 0; i <= 10 * frequency; i++)
            {
                clock.Seconds = i / (double)frequency;
                Assert.Equal(clock.Seconds * rate, surface.Position, 8);
            }

            Assert.Equal(rate, surface.Sample(true)!.Rate);
            surface.Seek(300);
            surface.Toggle();
            Assert.Equal(0, surface.Position);
            Assert.True(surface.Playing);
            surface.Replace(FiveMinuteRecording.Create());
            Assert.Equal(rate, surface.Rate);
            Assert.Equal(0, surface.Position);
            Assert.False(surface.Playing);
        }
    }

    [Fact]
    public void Fast_playback_does_not_raise_native_snapshot_cadence()
    {
        foreach (var rate in new[] { 1d, 2, 3, 5 })
        {
            var clock = new Clock();
            using var surface = new ReplayPresentation(FiveMinuteRecording.Create(), clock);
            surface.SetRate(rate);
            surface.Play();
            for (var i = 0; i <= 600; i++) { clock.Seconds = i / 60d; surface.Sample(); }
            Assert.Equal(41, surface.SnapshotComputations);
        }
    }

    [Fact]
    public void Recorded_ai_cues_have_exact_provenance_evidence_timing_and_seek_safe_goal_selection()
    {
        var match = FiveMinuteRecording.Create();
        var track = match.Commentary!;
        Assert.Equal("gpt-6.1-sol", track.Model);
        Assert.Equal(42, track.Cues.Count);
        Assert.Equal("82776c91906bc3727322c84b71943affebf823a36509d15a35c1ec916d1c9bf8", CommentaryPrompt.Hash(match));
        foreach (var goal in match.Events.Where(e => e.Kind == EventKind.Goal))
        {
            var cue = CommentaryReplay.At(track, goal.Time)!;
            Assert.Equal(goal.Id, cue.EventId);
            Assert.Equal(goal.Id, GoalReplay.At(match, goal.Time)!.EventId);
            Assert.NotEqual(goal.Id, CommentaryReplay.At(track, goal.Time - .001)?.EventId);
        }
        foreach (var time in new[] { 0d, 3, 49, 120, 194, 226, 299, 0, 50, 300 })
        {
            var expected = track.Cues.SingleOrDefault(c => c.Start <= time && time < c.End);
            Assert.Equal(expected, CommentaryReplay.At(track, time));
            Assert.Null(CommentaryReplay.At(track, time, false));
        }
    }

    [Theory]
    [InlineData("early")]
    [InlineData("overlap")]
    [InlineData("unknown")]
    [InlineData("player")]
    [InlineData("duration")]
    [InlineData("markup")]
    [InlineData("statistics")]
    [InlineData("future")]
    [InlineData("false-goal")]
    public void Invalid_commentary_cannot_be_attached_to_a_match(string invalid)
    {
        var match = FiveMinuteRecording.Create();
        var cues = match.Commentary!.Cues.ToArray();
        cues[0] = invalid switch
        {
            "early" => cues[0] with { Start = 2 },
            "overlap" => cues[0] with { End = 12 },
            "unknown" => cues[0] with { EventId = "unknown" },
            "player" => cues[0] with { PlayerIds = ["arsenal-saka"] },
            "duration" => cues[0] with { End = 301 },
            "markup" => cues[0] with { Text = "<script>unsafe</script>" },
            "statistics" => cues[0] with { Text = "That is 99 percent passing." },
            "future" => cues[0] with { Text = "Arsenal will score later." },
            _ => cues[0] with { Text = "Goal! Arsenal scores!" }
        };
        Assert.Throws<ArgumentException>(() => match.WithCommentary(cues, "gpt-6.1-sol"));
    }

    [Fact]
    public void Individually_valid_cues_cannot_overlap_or_duplicate_identifiers()
    {
        var match = FiveMinuteRecording.Create();
        var cues = match.Commentary!.Cues.ToArray();
        cues[4] = cues[4] with { End = 39 };
        Assert.Throws<ArgumentException>(() => match.WithCommentary(cues, "gpt-6.1-sol"));
        cues = match.Commentary.Cues.ToArray();
        cues[1] = cues[1] with { Id = cues[0].Id };
        Assert.Throws<ArgumentException>(() => match.WithCommentary(cues, "gpt-6.1-sol"));
    }

    [Fact]
    public async Task New_match_contract_requires_its_own_valid_cues_and_failure_keeps_last_track()
    {
        var initial = AuthoredMatchRecording.Create();
        var plan = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json());
        Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(plan));
        var json = JsonSerializer.Serialize(plan with { Commentary = initial.Commentary!.Cues.ToArray() }, AuthoredJsonContext.Default.AuthoredMatchDocument);
        var built = AuthoredMatchPlan.Parse(json);
        Assert.Equal(41, built.Commentary!.Cues.Count);
        using var schema = JsonDocument.Parse(AuthoredMatchPrompt.Schema);
        Assert.Equal(22, schema.RootElement.GetProperty("properties").GetProperty("players").GetProperty("minItems").GetInt32());
        var service = new GenerationService(new Source(AuthoredMatchRecording.Json()), initial, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Same(initial, service.Current);
        Assert.Same(initial.Commentary, service.Current.Commentary);
    }

    [Fact]
    public void Commentary_recording_rejects_wrong_match_hash_duplicate_properties_and_unknown_fields()
    {
        var match = FiveMinuteRecording.Create();
        var json = FiveMinuteRecording.CommentaryJson();
        Assert.Throws<ScenarioValidationException>(() => CommentaryPrompt.Attach(json.Replace(CommentaryPrompt.Hash(match), "wrong"), match));
        Assert.Throws<ScenarioValidationException>(() => CommentaryPrompt.Attach(json.Replace("\"cues\":", "\"extra\":true,\"cues\":"), match));
        Assert.Throws<ScenarioValidationException>(() => CommentaryPrompt.Attach(json.Replace("\"cues\":", "\"cues\":[],\"cues\":"), match));
    }

    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
    private sealed class Source(string json) : IScenarioSource
    {
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken) => Task.FromResult(json);
    }
}
