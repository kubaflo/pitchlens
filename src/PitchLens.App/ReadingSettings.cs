using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitchLens.App;

public sealed record ReadingSettings([property:JsonRequired] int Version=1,[property:JsonRequired] int Percent=100)
{
    public static ReadingSettings Default{get;}=new();
    public void Validate()
    {if(Version!=1||Percent is <100 or >200||Percent%25!=0)throw new ArgumentException("Choose a text size from 100% to 200% in 25% steps.");}
    public string Css=>":root{--reading-scale:"+((double)Percent/100).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+";}";
    public static string Serialize(ReadingSettings value){value.Validate();return JsonSerializer.Serialize(value,ReadingJsonContext.Default.ReadingSettings);}
    public static ReadingSettings Load(string? json,out string? notice)
    {
        notice=null;if(string.IsNullOrEmpty(json))return Default;
        try
        {
            if(json.Length>256)throw new ArgumentException("Oversized text preferences.");
            var options=new JsonSerializerOptions(ReadingJsonContext.Default.Options){AllowDuplicateProperties=false,MaxDepth=4};
            var value=JsonSerializer.Deserialize(json,new ReadingJsonContext(options).ReadingSettings)??throw new ArgumentException("Empty text preferences.");
            value.Validate();return value;
        }
        catch(Exception error) when(error is JsonException or ArgumentException)
        {notice="Saved text size is invalid. Standard 100% is active; choose a size or Reset to replace it.";return Default;}
    }
}
[JsonSourceGenerationOptions(PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ReadingSettings))]
public partial class ReadingJsonContext:JsonSerializerContext;
