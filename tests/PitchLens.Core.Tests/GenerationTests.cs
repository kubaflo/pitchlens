using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class GenerationTests
{
    private static ScenarioDocument Valid()
    {
        var match = PremierLeagueRoster.CreateDeterministicDemo();
        return new(72, match.OwnerAt(0)!,
            match.Players.Select(p => new PlayerDocument(p.Id,
                p.Path.Frames.Select(f => new FrameDocument(f.Time, f.Position.X, f.Position.Y)).ToArray())).ToArray(),
            match.Events.Select(e => new EventDocument(e.Id, e.Time, e.Order, e.Kind.ToString().ToLowerInvariant(),
                e.ActorId, e.ReceiverId, e.Completed, e.StartTime)).ToArray());
    }

    private static string Json(ScenarioDocument scenario) =>
        JsonSerializer.Serialize(scenario, ScenarioJsonContext.Default.ScenarioDocument);

    [Fact]
    public void Valid_roster_scenario_maps_to_coherent_deterministic_replay()
    {
        var scenario = Valid();
        var match = ScenarioValidator.Parse(Json(scenario));
        Assert.Equal(12, match.Players.Count);
        Assert.Equal(PremierLeagueRoster.Players.Select(p => p.Id), match.Players.Select(p => p.Id));
        Assert.Equal("David Raya", match.Player("arsenal-raya").Name);
        Assert.Equal(41, match.Player("arsenal-rice").Number);
        Assert.Equal(1, Replay.At(match, 72).Score["arsenal"]);
        Assert.Equal(0, Replay.At(match, 55).Score["arsenal"]);
        for (var i = 0; i <= 720; i++)
        {
            var time = i / 10d;
            var owner = match.OwnerAt(time);
            if (owner is not null)
                Assert.True(match.Player(owner).Path.At(time).DistanceTo(match.Ball.At(time)) < 1e-9);
        }
        var again = ScenarioValidator.Parse(Json(scenario));
        Assert.Equal(Replay.At(match, 39).Players, Replay.At(again, 39).Players);
        Assert.All(Replay.At(match, 72).Players, p => Assert.InRange(p.MaxSpeed, 0, 8));
        Assert.Equal(new Position(105, 34), match.Ball.At(56));
    }

    [Fact]
    public void Model_player_order_is_canonicalized_to_keep_roster_selection_correct()
    {
        var document = Valid();
        var match = ScenarioValidator.Build(document with { Players = document.Players.Reverse().ToArray() });
        Assert.Equal(PremierLeagueRoster.Players.Select(p => p.Id), match.Players.Select(p => p.Id));
        Assert.Equal(1, Replay.At(match, 72).Score["arsenal"]);
    }

    [Fact]
    public void Duplicate_full_json_properties_are_rejected()
    {
        var json = Json(Valid()).Replace("\"duration\":72", "\"duration\":72,\"duration\":72");
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Parse(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("```json\n{}\n```")]
    [InlineData("{\"duration\":72,\"duration\":72}")]
    public void Malformed_or_missing_schema_output_is_rejected(string json) =>
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Parse(json));

    [Fact]
    public void Extra_properties_and_null_collections_are_rejected()
    {
        var json = Json(Valid());
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Parse(json.Replace("\"duration\":72", "\"extra\":1,\"duration\":72")));
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(Valid() with { Players = null! }));
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(Valid() with { Events = null! }));
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Parse(new string('x', 200001)));
    }

    [Fact]
    public void Unknown_roster_ids_and_duplicate_players_are_rejected()
    {
        var document = Valid();
        var players = document.Players.ToArray();
        players[0] = players[0] with { Id = "invented-player" };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        players[0] = players[1];
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { InitialOwnerId = "unknown" }));
    }

    [Fact]
    public void Causal_mismatch_and_overlapping_actions_are_rejected()
    {
        var document = Valid();
        var events = document.Events.ToArray();
        events[0] = events[0] with { ActorId = "arsenal-rice" };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Events = events }));
        events = document.Events.ToArray();
        events[1] = events[1] with { StartTime = 7 };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Events = events }));
        events = document.Events.ToArray();
        events[0] = events[0] with { Completed = false };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Events = events }));
    }

    [Fact]
    public void Shot_goal_and_kickoff_consistency_is_required()
    {
        var document = Valid();
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with
        { Events = document.Events.Where(e => e.Kind != "goal").ToArray() }));
        var events = document.Events.ToArray();
        events[8] = events[8] with { ActorId = "arsenal-raya" };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Events = events }));
        events = document.Events.ToArray();
        events[8] = events[8] with { Time = 57 };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Events = events }));
    }

    [Fact]
    public void Impossible_player_speed_coordinates_and_time_are_rejected()
    {
        var document = Valid();
        var frames = document.Players[0].Frames.ToArray();
        frames[1] = frames[1] with { X = 104, Y = 67 };
        var players = document.Players.ToArray();
        players[0] = players[0] with { Frames = frames };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        frames[1] = frames[1] with { X = 106 };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        frames[1] = frames[1] with { Time = 0 };
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Duration = 73 }));
    }

    [Fact]
    public void Excessive_ball_flight_speed_is_rejected_before_match_replacement()
    {
        var document = Valid();
        var players = document.Players.Select(p => p.Id switch
        {
            "arsenal-raya" => p with { Frames = [new(0, 0, 0), new(72, 0, 0)] },
            "arsenal-odegaard" => p with { Frames = [new(0, 105, 68), new(72, 105, 68)] },
            _ => p
        }).ToArray();
        var error = Assert.Throws<ScenarioValidationException>(() => ScenarioValidator.Build(document with { Players = players }));
        Assert.Contains("40 m/s", error.Message);
    }

    [Fact]
    public void Generated_backward_carries_do_not_get_unverified_forward_commentary()
    {
        var document = Valid();
        var players = document.Players.Select(p => p.Id == "arsenal-odegaard"
            ? p with { Frames = p.Frames.Select(f => f with { X = 105 - f.X }).ToArray() } : p).ToArray();
        var match = ScenarioValidator.Build(document with { Players = players });
        var carry = Explanations.At(match, 42, ExplanationMode.Casual).Single(e => e.EventId == "E05");
        Assert.Equal("Martin Odegaard carries the ball.", carry.Text);
    }

    [Fact]
    public async Task Successful_fake_response_commits_only_after_validation()
    {
        var initial = PremierLeagueRoster.CreateDeterministicDemo();
        var source = new FakeSource((_, _) => Task.FromResult(Json(Valid())));
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(1, source.Calls);
        Assert.Equal(GenerationState.Succeeded, service.State);
        Assert.True(service.IsSdkGenerated);
        Assert.NotSame(initial, service.Current);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("auth")]
    [InlineData("timeout")]
    public async Task Failure_keeps_last_valid_match_and_is_explicit(string failure)
    {
        var initial = PremierLeagueRoster.CreateDeterministicDemo();
        var source = new FakeSource((_, _) => failure switch
        {
            "auth" => Task.FromException<string>(new CopilotGenerationException("Authentication required.")),
            "timeout" => Task.FromException<string>(new OperationCanceledException()),
            _ => Task.FromResult("{}")
        });
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Same(initial, service.Current);
        Assert.False(service.IsSdkGenerated);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Contains("Previous match kept", service.Status);
    }

    [Fact]
    public async Task Cancellation_and_concurrent_request_do_not_replace_last_match()
    {
        var initial = PremierLeagueRoster.CreateDeterministicDemo();
        var source = new FakeSource(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Json(Valid());
        });
        var service = new GenerationService(source, initial);
        using var cancel = new CancellationTokenSource();
        var running = service.GenerateAsync(null, cancel.Token);
        Assert.Equal(GenerationState.Generating, service.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(null, CancellationToken.None));
        cancel.Cancel();
        await running;
        Assert.Equal(GenerationState.Cancelled, service.State);
        Assert.Same(initial, service.Current);
        Assert.False(service.IsSdkGenerated);
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task Cancellation_after_response_and_failure_after_success_keep_committed_scenario()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var source = new FakeSource((_, _) =>
        {
            if (++calls == 1) return Task.FromResult(Json(Valid()));
            cancellation.Cancel();
            return Task.FromResult(Json(Valid()));
        });
        var service = new GenerationService(source, PremierLeagueRoster.CreateDeterministicDemo());
        await service.GenerateAsync(null, CancellationToken.None);
        var valid = service.Current;
        await service.GenerateAsync(null, cancellation.Token);
        Assert.Equal(GenerationState.Cancelled, service.State);
        Assert.Same(valid, service.Current);
        Assert.True(service.IsSdkGenerated);
    }

    [Fact]
    public async Task Authentication_failure_after_success_keeps_last_generated_match_and_provenance()
    {
        var calls = 0;
        var source = new FakeSource((_, _) => ++calls == 1 ? Task.FromResult(Json(Valid()))
            : Task.FromException<string>(new CopilotGenerationException("Authentication required.")));
        var service = new GenerationService(source, PremierLeagueRoster.CreateDeterministicDemo());
        await service.GenerateAsync(null, CancellationToken.None);
        var generated = service.Current;
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Same(generated, service.Current);
        Assert.True(service.IsSdkGenerated);
        Assert.Contains("Authentication required", service.Status);
    }

    [Fact]
    public async Task Missing_runtime_is_a_visible_prerequisite_error_without_requesting_a_model()
    {
        var service = new GenerationService(new CopilotScenarioSource("/not-installed/copilot", "/not-used"),
            PremierLeagueRoster.CreateDeterministicDemo());
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State);
        Assert.Contains("executable is missing", service.Status);
        Assert.False(service.IsSdkGenerated);
    }

    [Fact]
    public async Task Cancellation_retains_priority_and_surfaces_a_runtime_cleanup_error()
    {
        using var cancellation = new CancellationTokenSource();
        var initial = PremierLeagueRoster.CreateDeterministicDemo();
        var source = new FakeSource((_, _) =>
        {
            cancellation.Cancel();
            return Task.FromException<string>(new CopilotGenerationException("Session cleanup reported an error."));
        });
        var service = new GenerationService(source, initial);
        await service.GenerateAsync(null, cancellation.Token);
        Assert.Equal(GenerationState.Cancelled, service.State);
        Assert.Same(initial, service.Current);
        Assert.Contains("cleanup reported an error", service.Status);
    }

    private sealed class FakeSource(Func<IProgress<string>?, CancellationToken, Task<string>> action) : IScenarioSource
    {
        public int Calls { get; private set; }
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            return action(progress, cancellationToken);
        }
    }
}
