using Microsoft.Maui.Graphics;
using PitchLens.Core;

namespace PitchLens.App;

public sealed class PitchDrawable(Match match) : IDrawable
{
    public Match Match { get; set; } = match;
    public double Time { get; set; }
    public string? SelectedId { get; set; }
    public const double PlayerRadius = 2;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var match = Match;
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0) return;
        var transform = PitchTransform.Fit(dirtyRect.Width, dirtyRect.Height);
        var scale = (float)transform.Scale;
        canvas.SaveState();
        canvas.FillColor = Color.FromArgb("#242333");
        canvas.FillRectangle(dirtyRect);
        canvas.Translate((float)transform.Left, (float)transform.Top);
        canvas.Scale(scale, scale);
        canvas.FillColor = Color.FromArgb("#4B7355");
        canvas.FillRectangle(0, 0, 105, 68);
        canvas.FillColor = Color.FromArgb("#50795A");
        for (var i = 0; i < 10; i += 2) canvas.FillRectangle(i * 10.5f, 0, 10.5f, 68);
        canvas.StrokeColor = Color.FromArgb("#D2DFD0");
        canvas.StrokeSize = .25f;
        canvas.DrawRectangle(0, 0, 105, 68);
        canvas.DrawLine(52.5f, 0, 52.5f, 68);
        canvas.DrawCircle(52.5f, 34, 9.15f);
        canvas.DrawRectangle(0, 13.84f, 16.5f, 40.32f);
        canvas.DrawRectangle(88.5f, 13.84f, 16.5f, 40.32f);
        canvas.DrawRectangle(0, 24.84f, 5.5f, 18.32f);
        canvas.DrawRectangle(99.5f, 24.84f, 5.5f, 18.32f);
        canvas.StrokeSize = .65f;
        canvas.DrawLine(0, 30.34f, 0, 37.66f);
        canvas.DrawLine(105, 30.34f, 105, 37.66f);

        if (SelectedId is not null)
        {
            var selected = match.Player(SelectedId);
            canvas.StrokeColor = MatchTheme.Lilac;
            canvas.StrokeSize = .4f;
            var points = selected.Path.Frames.Where(f => f.Time < Time).Select(f => f.Position)
                .Append(selected.Path.At(Time)).ToArray();
            for (var i = 1; i < points.Length; i++)
                canvas.DrawLine((float)points[i - 1].X, (float)points[i - 1].Y, (float)points[i].X, (float)points[i].Y);
        }
        // Draw ordinal IDs in reverse so the hit-test tie winner is visually on top.
        foreach (var player in match.Players.OrderByDescending(p => p.Id, StringComparer.Ordinal))
        {
            var p = player.Path.At(Time);
            if (player.Id == SelectedId)
            {
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = .6f;
                canvas.DrawCircle((float)p.X, (float)p.Y, 2.8f);
            }
            canvas.FillColor = Color.FromArgb("#23382B");
            canvas.FillCircle((float)p.X + .15f, (float)p.Y + .3f, (float)PlayerRadius + .2f);
            canvas.FillColor = MatchTheme.Team(player.TeamId);
            canvas.FillCircle((float)p.X, (float)p.Y, (float)PlayerRadius);
            canvas.StrokeColor = Color.FromArgb("#F4F4EF");
            canvas.StrokeSize = .18f;
            canvas.DrawCircle((float)p.X, (float)p.Y, (float)PlayerRadius);
            canvas.FontColor = MatchTheme.Purple;
            canvas.FontSize = 2.5f;
            canvas.DrawString(player.Number.ToString(), (float)p.X - 2, (float)p.Y - 2, 4, 4,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        var ball = match.Ball.At(Time);
        canvas.FillColor = Colors.White;
        canvas.FillCircle((float)ball.X, (float)ball.Y, .85f);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = .2f;
        canvas.DrawCircle((float)ball.X, (float)ball.Y, .85f);
        canvas.RestoreState();
    }
}
