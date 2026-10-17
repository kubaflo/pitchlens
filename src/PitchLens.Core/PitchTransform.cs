namespace PitchLens.Core;

public readonly record struct PitchTransform(double Scale, double Left, double Top)
{
    public static PitchTransform Fit(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport dimensions must be finite and positive.");
        var scale = Math.Min(width / Match.PitchWidth, height / Match.PitchHeight);
        return new(scale, (width - Match.PitchWidth * scale) / 2, (height - Match.PitchHeight * scale) / 2);
    }

    public Position ToScreen(Position pitch) => new(Left + pitch.X * Scale, Top + pitch.Y * Scale);
    public Position? ToPitch(Position screen)
    {
        if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) ||
            screen.X < Left || screen.Y < Top ||
            screen.X > Left + Match.PitchWidth * Scale || screen.Y > Top + Match.PitchHeight * Scale)
            return null;
        var p = new Position((screen.X - Left) / Scale, (screen.Y - Top) / Scale);
        return new(Math.Clamp(p.X, 0, Match.PitchWidth), Math.Clamp(p.Y, 0, Match.PitchHeight));
    }

    public string? HitTest(IEnumerable<(string Id, Position Position)> players, Position screen, double radius = 2)
    {
        if (!double.IsFinite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        var pitch = ToPitch(screen);
        if (pitch is null) return null;
        // Exact ties use the ordinal ID, independent of roster or drawing order.
        return players.Select(p => (p.Id, Distance: p.Position.DistanceTo(pitch.Value)))
            .Where(p => p.Distance <= radius).OrderBy(p => p.Distance).ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => p.Id).FirstOrDefault();
    }
}
