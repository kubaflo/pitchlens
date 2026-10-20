using System.Text.Json;
using PitchLens.Core;
using PitchLens.Generation;
using PitchLens.App;

namespace PitchLens.Core.Tests;

public sealed class NarrationTests
{
    [Fact]
    public void Narrative_evidence_navigation_is_canonical_and_stale_revision_cannot_seek_a_new_scene()
    {
        using var native = new MatchService(new Source("unused"), new NoMoment(), (_, _) => Task.CompletedTask);
        var match = native.Match;
        var goal = match.Narrative!.Chapters.First(c => c.Kind == "goal");
        var evidence = MomentEvidence.Capture(match, 1, goal.RevealTime, goal.FocusEventId, goal.EventIds);
        native.InspectEvidence(evidence, goal.FocusEventId);
        Assert.Equal(86, native.Sample(true)!.Frame.Snapshot.Time);
        Assert.False(native.Sample(true)!.Frame.Playing);
        var stale = MomentEvidence.Capture(match, 2, goal.RevealTime, goal.FocusEventId, goal.EventIds);
        Assert.Throws<InvalidOperationException>(() => native.InspectEvidence(stale, goal.FocusEventId));
        Assert.Equal(86, native.Sample(true)!.Frame.Snapshot.Time);
    }
    [Fact]
    public void Actual_recorded_package_has_matching_facts_cues_chapters_and_original_paths()
    {
        var match = NarrationRecording.Create();
        Assert.Equal(NarrationRecording.FactsHash, NarrationPrompt.Hash(match));
        Assert.Equal(40, match.Commentary!.Cues.Count);
        Assert.Equal(12, match.Narrative!.Chapters.Count);
        Assert.Equal(2, match.Narrative.Chapters.Count(c => c.Kind == "goal"));
        Assert.Equal(new[] { 86d, 176d }, match.Narrative.Chapters.Where(c => c.Kind == "goal").Select(c => c.RevealTime));
        Assert.Equal(Base().Events, match.Events);
        Assert.Equal(Base().Players.SelectMany(p => p.Path.Frames), match.Players.SelectMany(p => p.Path.Frames));
        Assert.True(MomentRecording.Available(match, 1));
        foreach (var time in new[] { 0d, 85.9, 176, 120, 86, 300, 0 })
        {
            Assert.All(StoryReplay.At(match, time), c => Assert.True(c.Time <= time));
            Assert.All(NarrativeReplay.At(match, time), c => Assert.True(c.RevealTime <= time));
        }
    }
    private static Match Base() => AuthoredMatchRecording.Create();
    private static string Json(NarrationDocument d) => JsonSerializer.Serialize(d, NarrationJsonContext.Default.NarrationDocument);

    [Fact]
    public void Package_binds_captions_story_geometry_and_as_of_reveal_to_one_measured_hash()
    {
        var original = Base();
        var document = NarrationFixtures.Package(original);
        var match = NarrationPrompt.Attach(Json(document), original);
        Assert.Equal(document.FactsHash, match.Narrative!.FactsHash);
        Assert.Equal(document.FactsHash, match.Commentary!.FactsHash);
        Assert.Equal(original.Events, match.Events);
        Assert.Equal(original.Players.SelectMany(p => p.Path.Frames), match.Players.SelectMany(p => p.Path.Frames));
        foreach (var chapter in match.Narrative.Chapters)
        {
            Assert.DoesNotContain(NarrativeReplay.At(match, chapter.RevealTime - .001), c => c.Id == chapter.Id);
            Assert.Contains(NarrativeReplay.At(match, chapter.RevealTime), c => c.Id == chapter.Id);
            Assert.All(chapter.EventIds, id => Assert.True(match.Events.Single(e => e.Id == id).Time <= chapter.RevealTime));
        }
        Assert.Empty(NarrativeReplay.At(match, 0));
        Assert.Equal("Takes the lead", NarrativeReplay.Consequence(match, match.Narrative.Chapters.Single(c => c.FocusEventId == "e28")));
        Assert.Equal("Level again", NarrativeReplay.Consequence(match, match.Narrative.Chapters.Last(c => c.Kind == "goal")));
    }

    [Fact]
    public void Future_context_unknown_refs_wrong_identity_and_invalid_text_fail_without_weakening_gates()
    {
        var match = Base(); var package = NarrationFixtures.Package(match);
        var chapter = package.Chapters[0];
        foreach (var bad in new[]
        {
            chapter with { EventIds = [chapter.FocusEventId, "e28"] },
            chapter with { EventIds = [chapter.FocusEventId, "unknown"] },
            chapter with { RevealTime = chapter.RevealTime - .1 },
            chapter with { PlayerIds = ["unknown"] },
            chapter with { Fan = "A total of 99 completed passes." },
            chapter with { Heading = "The eventual winner" },
            chapter with { Analyst = "<b>Untrusted markup</b>" }
        })
        {
            var chapters = package.Chapters.ToArray(); chapters[0] = bad;
            Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Chapters = chapters }, match));
        }
        var cue = package.Cues[0];
        var cues = package.Cues.ToArray(); cues[0] = cue with { EvidenceIds = [cue.EventId, "e28"] };
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Cues = cues }, match));
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { FactsHash = "wrong" }, match));
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(Json(package).Insert(1, "\"extra\":1,"), match));
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(Json(package).Insert(1, "\"factsHash\":\"wrong\","), match));
    }

    [Fact]
    public void Contextual_cue_can_resolve_prior_participant_but_never_a_future_payoff()
    {
        var match = Base(); var package = NarrationFixtures.Package(match);
        var index = Array.FindIndex(package.Cues, c => c.EventId == "e28");
        var cues = package.Cues.ToArray();
        cues[index] = cues[index] with
        {
            Text = "Saka's carry, the inward pass, then the goal.",
            EvidenceIds = ["e25", "e26", "e27", "e28"],
            PlayerIds = ["arsenal-saka", "arsenal-gyokeres"]
        };
        var attached = NarrationPrompt.Attach(package with { Cues = cues }, match);
        var goalCue = attached.Commentary!.Cues[index];
        Assert.NotEqual(goalCue, CommentaryReplay.At(attached.Commentary, goalCue.Start - .001));
        Assert.Equal(goalCue, CommentaryReplay.At(attached.Commentary, goalCue.Start));
        Assert.Null(CommentaryReplay.At(attached.Commentary, goalCue.End));
        var firstPass = package.Cues[0];
        cues[0] = firstPass with { EvidenceIds = [firstPass.EventId, "e28"] };
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Cues = cues }, match));
    }

    [Fact]
    public void Local_audience_switch_backward_seek_pause_and_restart_never_reveal_future_chapters()
    {
        var match = NarrationPrompt.Attach(NarrationFixtures.Package(Base()), Base());
        var clock = new Clock();
        using var surface = new ReplayPresentation(match, clock);
        surface.Seek(176);
        var fan = surface.Sample(true)!;
        surface.SetMode(ExplanationMode.Analyst);
        var analyst = surface.Sample(true)!;
        Assert.Equal(fan.Story.Select(c => c.EventIds), analyst.Story.Select(c => c.EventIds));
        Assert.NotEqual(fan.Story.Last().Detail, analyst.Story.Last().Detail);
        surface.Seek(85.9);
        Assert.DoesNotContain(surface.Sample(true)!.Story, s => s.EventIds.Contains("e28"));
        surface.Pause(); clock.Seconds = 500;
        Assert.Equal(85.9, surface.Position);
        surface.Restart();
        Assert.Empty(surface.Sample(true)!.Story);
    }

    [Fact]
    public async Task Caption_only_candidate_is_not_a_new_story_success_and_invalid_package_retains_atomic_last_valid()
    {
        var original = Base();
        var old = AuthoredMatchPlan.Document(AuthoredMatchRecording.Json()) with { Commentary = original.Commentary!.Cues.ToArray() };
        var source = new Source(JsonSerializer.Serialize(old, AuthoredJsonContext.Default.AuthoredMatchDocument));
        var service = new GenerationService(source, original);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Equal(GenerationState.Failed, service.State); Assert.Same(original, service.Current);
        var document = old with { Commentary = null, Narration = NarrationFixtures.Package(original) };
        Assert.Throws<ScenarioValidationException>(() => AuthoredMatchPlan.Build(document with { Commentary = original.Commentary.Cues.ToArray() }));
        source.Json = JsonSerializer.Serialize(document, AuthoredJsonContext.Default.AuthoredMatchDocument);
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.NotNull(service.Current.Narrative);
        var valid = service.Current;
        source.Json = source.Json.Replace(NarrationPrompt.Hash(original), "wrong");
        await service.GenerateAsync(null, CancellationToken.None);
        Assert.Same(valid, service.Current);
        Assert.Same(valid.Commentary, service.Current.Commentary);
        Assert.Same(valid.Narrative, service.Current.Narrative);
    }
    private sealed class Clock : IMonotonicClock { public double Seconds { get; set; } }
    private sealed class Source(string json) : IScenarioSource
    {
        public string Json { get; set; } = json;
        public Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken token) => Task.FromResult(Json);
    }
    private sealed class NoMoment : IMomentSource
    {
        public Task<string> ExplainAsync(MomentEvidence evidence, CancellationToken token) => throw new InvalidOperationException("No paid calls.");
    }
}
