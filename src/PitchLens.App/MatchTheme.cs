namespace PitchLens.App;

internal static class MatchTheme
{
    public static readonly Color Purple = Color.FromArgb("#37003C");
    public static readonly Color Mint = Color.FromArgb("#00FF87");
    public static readonly Color Pink = Color.FromArgb("#FF2882");
    public static readonly Color Lilac = Color.FromArgb("#D8B9F0");

    public static Color Team(string teamId) => teamId == "harbor" ? Mint : Pink;
}
