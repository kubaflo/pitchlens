using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitchLens.App;

public enum PlayerMarkerStyle { Numbers, PhotoTokens, CutOutPlayers, PortraitTiles, FocusFirst, SmartZoom }
public sealed record PitchViewSettings(
    [property:JsonRequired] int Version=1,
    [property:JsonRequired] PlayerMarkerStyle Style=PlayerMarkerStyle.Numbers,
    [property:JsonRequired] double MarkerScale=1,
    [property:JsonRequired] bool ShowNumbers=true,
    [property:JsonRequired] bool SmartDetail=false)
{
    public static PitchViewSettings Default{get;}=new();
    public void Validate()
    {
        if(Version!=1||!Enum.IsDefined(Style)||!double.IsFinite(MarkerScale)||MarkerScale is <.75 or >1.5)
            throw new ArgumentException("View settings require a known style, version1 and marker size75–150%.");
    }
    public static string Serialize(PitchViewSettings value)
    {value.Validate();return JsonSerializer.Serialize(value,PitchViewJsonContext.Default.PitchViewSettings);}
    public static PitchViewSettings Load(string? json,out string? notice)
    {
        notice=null;
        if(string.IsNullOrEmpty(json))return Default;
        try
        {
            if(json.Length>1000)throw new ArgumentException("Saved view settings are oversized.");
            var options=new JsonSerializerOptions(PitchViewJsonContext.Default.Options){AllowDuplicateProperties=false,MaxDepth=8};
            var value=JsonSerializer.Deserialize(json,new PitchViewJsonContext(options).PitchViewSettings)
                ??throw new ArgumentException("Saved view settings are empty.");
            value.Validate();return value;
        }
        catch(Exception e) when(e is JsonException or ArgumentException)
        {
            notice="Saved view settings are invalid. Numbers defaults are active; choose settings or Reset to replace them.";
            return Default;
        }
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(PitchViewSettings))]
public partial class PitchViewJsonContext:JsonSerializerContext;
