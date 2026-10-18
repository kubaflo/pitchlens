using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record RosterPlayer(string Id, string TeamId, string Name, int Number);

public static class PremierLeagueRoster
{
    public const string Season = "2026/27";
    public const string VerifiedOn = "2026-10-18";
    public static IReadOnlyList<Team> Teams { get; } = Array.AsReadOnly(new[]
    {
        new Team("arsenal", "Arsenal"), new Team("manchester-city", "Manchester City")
    });
    public static IReadOnlyList<RosterPlayer> Players { get; } = Array.AsReadOnly(new[]
    {
        new RosterPlayer("arsenal-raya", "arsenal", "David Raya", 1),
        new("arsenal-saliba", "arsenal", "William Saliba", 2),
        new("arsenal-odegaard", "arsenal", "Martin Odegaard", 8),
        new("arsenal-rice", "arsenal", "Declan Rice", 41),
        new("arsenal-saka", "arsenal", "Bukayo Saka", 7),
        new("arsenal-gyokeres", "arsenal", "Viktor Gyokeres", 14),
        new("manchester-city-donnarumma", "manchester-city", "Gianluigi Donnarumma", 1),
        new("manchester-city-dias", "manchester-city", "Rúben Dias", 3),
        new("manchester-city-foden", "manchester-city", "Phil Foden", 47),
        new("manchester-city-cherki", "manchester-city", "Rayan Cherki", 10),
        new("manchester-city-haaland", "manchester-city", "Erling Haaland", 9),
        new("manchester-city-doku", "manchester-city", "Jérémy Doku", 11)
    });

    public static Match CreateDeterministicDemo()
    {
        var original = DemoMatch.Create();
        var ids = original.Players.Select((p, i) => (p.Id, NewId: Players[i].Id)).ToDictionary(p => p.Id, p => p.NewId);
        var players = original.Players.Select((p, i) =>
            new Player(Players[i].Id, Players[i].TeamId, Players[i].Number, Players[i].Name, p.Path));
        return new(original.Duration, Teams, players,
            original.Events.Select(e => e with { ActorId = ids[e.ActorId], ReceiverId = e.ReceiverId is null ? null : ids[e.ReceiverId] }),
            original.Ball, original.Possession.Select(p => p with { PlayerId = p.PlayerId is null ? null : ids[p.PlayerId] }));
    }
}
