namespace PitchLens.App;

internal static class MatchTheme
{
    public static readonly Color Purple = Color.FromArgb("#37003C");
    public static readonly Color Mint = Color.FromArgb("#00FF87");
    public static readonly Color Pink = Color.FromArgb("#FF2882");
    public static readonly Color Lilac = Color.FromArgb("#D8B9F0");

    public static Color Team(string teamId) => teamId == "harbor" ? Mint : Pink;
}

internal sealed class StadiumArtwork : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.StrokeColor = Color.FromArgb("#56205D");
        canvas.StrokeSize = 1;
        var x = dirtyRect.Width * .65f;
        var y = dirtyRect.Height * .5f;
        for (var i = 0; i < 5; i++)
        {
            var inset = i * 12;
            canvas.DrawRoundedRectangle(x - 125 + inset, y - 120 + inset,
                320 - inset * 2, 240 - inset * 2, 48);
        }
        canvas.StrokeColor = Color.FromArgb("#683572");
        canvas.DrawLine(dirtyRect.Width * .4f, dirtyRect.Height, dirtyRect.Width * .6f, 0);
        canvas.DrawLine(dirtyRect.Width * .42f, dirtyRect.Height, dirtyRect.Width * .62f, 0);
        canvas.RestoreState();
    }
}
