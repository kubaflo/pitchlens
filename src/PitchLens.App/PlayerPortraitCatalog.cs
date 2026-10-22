using System.Text.Json;
using System.Text.Json.Serialization;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.App;

public sealed record PlayerPortraitIdentity(string PlayerId,string TeamId,string Name,string OfficialName,string OfficialId,
    string? ImagePath,string? UnavailableReason);
public sealed record PlayerPortraitDocument(PlayerPortraitIdentity[] Entries);
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlayerPortraitDocument))]
public partial class PortraitJsonContext:JsonSerializerContext;

public static class PlayerPortraitCatalog
{
    public static IReadOnlyList<PlayerPortraitIdentity> Entries{get;}=Load();
    private static readonly IReadOnlyDictionary<string,PlayerPortraitIdentity> ById=Entries.ToDictionary(p=>p.PlayerId,StringComparer.Ordinal);
    public static PlayerPortraitIdentity? Find(Player player)=>
        ById.TryGetValue(player.Id,out var portrait)&&portrait.TeamId==player.TeamId&&portrait.Name==player.Name?portrait:null;

    private static IReadOnlyList<PlayerPortraitIdentity> Load()
    {
        using var stream=typeof(FixtureCatalog).Assembly.GetManifestResourceStream("PitchLens.Generation.player-portraits.json")
            ??throw new InvalidOperationException("Verified portrait identity resource is missing.");
        var document=JsonSerializer.Deserialize(stream,PortraitJsonContext.Default.PlayerPortraitDocument)
            ??throw new InvalidOperationException("Portrait identity resource is empty.");
        if(document.Entries is not{Length:>0 and <=1000}||document.Entries.Any(p=>p is null)||
            document.Entries.Select(p=>p.PlayerId).Distinct().Count()!=document.Entries.Length||
            document.Entries.Any(p=>string.IsNullOrWhiteSpace(p.PlayerId)||!p.PlayerId.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-')||
                p.ImagePath is not null&&p.ImagePath!=$"images/players/{p.PlayerId}.png"))
            throw new InvalidOperationException("Portrait identity resource has invalid coverage or nonlocal asset paths.");
        return Array.AsReadOnly(document.Entries);
    }
}
