using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record MatchRosterPlayer(string Id, string TeamId, string Name, int Number, string Role);

public static class RegulationRoster
{
    public const string VerifiedOn = "2026-10-19";
    public static IReadOnlyList<Team> Teams => PremierLeagueRoster.Teams;
    public static IReadOnlyList<MatchRosterPlayer> Players { get; } = Array.AsReadOnly(new[]
    {
        new MatchRosterPlayer("arsenal-raya", "arsenal", "David Raya", 1, "goalkeeper"),
        new("arsenal-timber", "arsenal", "Jurrien Timber", 12, "defender"),
        new("arsenal-saliba", "arsenal", "William Saliba", 2, "defender"),
        new("arsenal-gabriel", "arsenal", "Gabriel", 6, "defender"),
        new("arsenal-calafiori", "arsenal", "Riccardo Calafiori", 33, "defender"),
        new("arsenal-zubimendi", "arsenal", "Martin Zubimendi", 36, "midfielder"),
        new("arsenal-rice", "arsenal", "Declan Rice", 41, "midfielder"),
        new("arsenal-odegaard", "arsenal", "Martin Odegaard", 8, "midfielder"),
        new("arsenal-saka", "arsenal", "Bukayo Saka", 7, "forward"),
        new("arsenal-gyokeres", "arsenal", "Viktor Gyokeres", 14, "forward"),
        new("arsenal-martinelli", "arsenal", "Gabriel Martinelli", 11, "forward"),
        new("manchester-city-donnarumma", "manchester-city", "Gianluigi Donnarumma", 1, "goalkeeper"),
        new("manchester-city-lewis", "manchester-city", "Rico Lewis", 82, "defender"),
        new("manchester-city-dias", "manchester-city", "Rúben Dias", 3, "defender"),
        new("manchester-city-guehi", "manchester-city", "Marc Guéhi", 6, "defender"),
        new("manchester-city-ait-nouri", "manchester-city", "Rayan Aït-Nouri", 21, "defender"),
        new("manchester-city-anderson", "manchester-city", "Elliot Anderson", 5, "midfielder"),
        new("manchester-city-kovacic", "manchester-city", "Mateo Kovačić", 8, "midfielder"),
        new("manchester-city-cherki", "manchester-city", "Rayan Cherki", 10, "midfielder"),
        new("manchester-city-foden", "manchester-city", "Phil Foden", 47, "forward"),
        new("manchester-city-haaland", "manchester-city", "Erling Haaland", 9, "forward"),
        new("manchester-city-doku", "manchester-city", "Jérémy Doku", 11, "forward")
    });
    public static bool IsKeeper(string id) => id is "arsenal-raya" or "manchester-city-donnarumma";
}
