using PitchLens.Core;

namespace PitchLens.Core.Tests;

public sealed class PitchTransformTests
{
    [Theory]
    [InlineData(1200, 400)]
    [InlineData(400, 1200)]
    [InlineData(1050, 680)]
    public void Transform_preserves_aspect_and_round_trips(double width, double height)
    {
        var transform = PitchTransform.Fit(width, height);
        Assert.True(transform.Left >= 0);
        Assert.True(transform.Top >= 0);
        Assert.True(transform.Left == 0 || transform.Top == 0);
        Assert.Equal(width, transform.Left * 2 + 105 * transform.Scale, 8);
        Assert.Equal(height, transform.Top * 2 + 68 * transform.Scale, 8);
        foreach (var p in new[] { new Position(0, 0), new(105, 68), new(23.7, 61.3) })
        {
            var back = transform.ToPitch(transform.ToScreen(p));
            Assert.NotNull(back);
            Assert.Equal(p.X, back.Value.X, 8);
            Assert.Equal(p.Y, back.Value.Y, 8);
        }
    }

    [Theory]
    [InlineData(1200, 400)]
    [InlineData(400, 1200)]
    public void Letterboxes_do_not_select_players_even_at_pitch_edge(double width, double height)
    {
        var transform = PitchTransform.Fit(width, height);
        var edge = transform.ToScreen(new(0, 0));
        var margin = transform.Left > 0 ? new Position(edge.X - .01, edge.Y) : new Position(edge.X, edge.Y - .01);
        Assert.Null(transform.ToPitch(margin));
        Assert.Null(transform.HitTest(new[] { ("edge", new Position(0, 0)) }, margin));
        Assert.Equal("edge", transform.HitTest(new[] { ("edge", new Position(0, 0)) }, edge));
    }

    [Fact]
    public void Hit_test_uses_nearest_and_ordinal_tie_break_independent_of_input_order()
    {
        var transform = PitchTransform.Fit(1050, 680);
        var players = new[] { ("h2", new Position(30, 30)), ("h1", new Position(30, 30)),
            ("h3", new Position(32, 30)) };
        Assert.Equal("h1", transform.HitTest(players, transform.ToScreen(new(30, 30))));
        Assert.Equal("h1", transform.HitTest(players.Reverse(), transform.ToScreen(new(30, 30))));
        Assert.Equal("h3", transform.HitTest(players, transform.ToScreen(new(31.9, 30))));
        Assert.Null(transform.HitTest(players, transform.ToScreen(new(50, 50))));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, -1)]
    [InlineData(double.NaN, 100)]
    [InlineData(100, double.PositiveInfinity)]
    public void Invalid_viewports_are_rejected(double width, double height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PitchTransform.Fit(width, height));
}
