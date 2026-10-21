using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class ScoringLanguageTests
{
    [Theory]
    [InlineData("The carry moves toward the goal.")]
    [InlineData("The carry moves towards goal.")]
    [InlineData("The pass crosses the goal area.")]
    [InlineData("The ball moves across the goal-mouth.")]
    [InlineData("The pass travels beside the goal line.")]
    [InlineData("The receiver is in front of goal.")]
    [InlineData("Saka carries closer to goal.")]
    [InlineData("Closer to the GOAL!")]
    public void Spatial_goal_reference_is_not_a_scoring_claim(string text) =>
        Assert.False(NarrativeText.HasScoringClaim(text));

    [Theory]
    [InlineData("A goal changes the game.")]
    [InlineData("He scores from the goal area.")]
    [InlineData("The goal was scored after the pass.")]
    [InlineData("The opener arrives in front of goal.")]
    [InlineData("Two goals from that move.")]
    [InlineData("The equalizer comes from the goal mouth.")]
    [InlineData("No goal was scored.")]
    [InlineData("A goal? Not yet.")]
    [InlineData("Toward the goal, then scored.")]
    [InlineData("The carry moves toward the goal. The opener arrives.")]
    [InlineData("Closer to goal, then scored.")]
    [InlineData("Closer to the goal. A goal follows.")]
    [InlineData("Closer to goal. The opener arrives.")]
    [InlineData("Closer to goal, an equalizer!")]
    [InlineData("Not closer to goal.")]
    [InlineData("No closer to the goal.")]
    public void Actual_scoring_language_cannot_be_hidden_inside_a_spatial_phrase(string text) =>
        Assert.True(NarrativeText.HasScoringClaim(text));

    [Fact]
    public void Spatial_exception_needs_direction_or_location_in_observed_geometry_not_just_safe_words()
    {
        NarrativeText.GoalGeometry[] forward = [new([new(70,34),new(80,34)],true)];
        NarrativeText.GoalGeometry[] backward = [new([new(80,34),new(70,34)],true)];
        NarrativeText.GoalGeometry[] awayForward = [new([new(30,34),new(20,34)],false)];
        Assert.True(NarrativeText.SpatialGoalReferencesSupported("toward the goal",forward));
        Assert.True(NarrativeText.SpatialGoalReferencesSupported("toward the goal",awayForward));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported("toward the goal",backward));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported("across the goal area",forward));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported("in front of goal",forward));
        Assert.True(NarrativeText.SpatialGoalReferencesSupported("across the goal area",
            [new NarrativeText.GoalGeometry([new(101,34),new(102,35)],true)]));
        Assert.True(NarrativeText.SpatialGoalReferencesSupported("along the goal line",
            [new NarrativeText.GoalGeometry([new(0,34)],false)]));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported("across the goal mouth",[]));
        Assert.True(NarrativeText.HasScoringClaim("The goal is close."));
    }
    [Theory]
    [InlineData("closer to goal")]
    [InlineData("closer to the goal.")]
    [InlineData("CLOSER TO THE GOAL!")]
    public void Closer_to_goal_requires_the_same_observed_direction_proof_on_either_side(string text)
    {
        Assert.True(NarrativeText.SpatialGoalReferencesSupported(text,[new NarrativeText.GoalGeometry([new(70,34),new(80,34)],true)]));
        Assert.True(NarrativeText.SpatialGoalReferencesSupported(text,[new NarrativeText.GoalGeometry([new(30,34),new(20,34)],false)]));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported(text,[new NarrativeText.GoalGeometry([new(80,34),new(70,34)],true)]));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported(text,[new NarrativeText.GoalGeometry([new(20,34),new(30,34)],false)]));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported(text,[new NarrativeText.GoalGeometry([new(70,34),new(70,34)],true)]));
        Assert.False(NarrativeText.SpatialGoalReferencesSupported(text,[]));
        Assert.True(NarrativeText.HasScoringClaim(text+", then scored."));
    }

    [Fact]
    public void Same_package_red_to_green_only_for_observed_spatial_language_and_still_rejects_unseen_scoring()
    {
        var match = AuthoredMatchRecording.Create();
        var package = NarrationFixtures.Package(match);
        var chapters = package.Chapters.ToArray();
        chapters[0] = chapters[0] with { Analyst = "The observed carry takes the ball toward the goal." };
        Assert.DoesNotContain(chapters[0].EventIds, id => match.Events.Single(e => e.Id == id).Kind == EventKind.Goal);
        // The original token-only gate rejected the same valid spatial wording.
        Assert.Contains("goal", NarrativeText.Words(chapters[0].Analyst));
        var valid = NarrationPrompt.Attach(package with { Chapters = chapters }, match);
        Assert.NotNull(valid.Narrative);
        var cues = package.Cues.ToArray();
        cues[0] = cues[0] with { Text = "The ball travels toward the goal." };
        Assert.NotNull(NarrationPrompt.Attach(package with { Chapters = chapters, Cues = cues }, match).Commentary);
        chapters[0] = chapters[0] with { Analyst = "The pass reaches the goal area and he scores." };
        var failure = Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Chapters = chapters }, match));
        Assert.Contains(chapters[0].Id, failure.Message);
        Assert.Contains("observed goal evidence", failure.Message);
        cues[0] = cues[0] with { Text = "The opener arrives from the goal area." };
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Cues = cues }, match));
        var first = package.Chapters[0];
        chapters[0] = first with { Analyst = "The carry moves toward the goal.", EventIds = [first.FocusEventId, "e28"] };
        Assert.Throws<ScenarioValidationException>(() => NarrationPrompt.Attach(package with { Chapters = chapters }, match));
    }
}
