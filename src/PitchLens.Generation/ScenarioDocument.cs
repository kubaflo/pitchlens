using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitchLens.Generation;

public sealed record ScenarioDocument(
    [property: JsonRequired] double Duration,
    [property: JsonRequired] string InitialOwnerId,
    [property: JsonRequired] PlayerDocument[] Players,
    [property: JsonRequired] EventDocument[] Events,
    [property: JsonRequired] StoryDocument Story);
public sealed record StoryDocument(
    [property: JsonRequired] string Title,
    [property: JsonRequired] BeatDocument[] Beats);
public sealed record BeatDocument(
    [property: JsonRequired] string Id,
    [property: JsonRequired] double StartTime,
    [property: JsonRequired] double EndTime,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string[] EventIds,
    [property: JsonRequired] string[] PlayerIds);
public sealed record PlayerDocument(
    [property: JsonRequired] string Id,
    [property: JsonRequired] FrameDocument[] Frames);
public sealed record FrameDocument(
    [property: JsonRequired] double Time,
    [property: JsonRequired] double X,
    [property: JsonRequired] double Y);
public sealed record EventDocument(
    [property: JsonRequired] string Id,
    [property: JsonRequired] double Time,
    [property: JsonRequired] int Order,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string ActorId,
    [property: JsonRequired] string? ReceiverId,
    [property: JsonRequired] bool? Completed,
    [property: JsonRequired] double? StartTime);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ScenarioDocument))]
[JsonSerializable(typeof(RosterPlayer[]))]
public partial class ScenarioJsonContext : JsonSerializerContext;

public sealed class ScenarioValidationException(string message, Exception? inner = null) : Exception(message, inner);

public static class ScenarioParser
{
    private static readonly ScenarioJsonContext Context = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 32
    });

    public static ScenarioDocument ParseDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 200_000)
            throw new ScenarioValidationException("Copilot returned empty or oversized scenario JSON.");
        try
        {
            return JsonSerializer.Deserialize(json, Context.ScenarioDocument)
                ?? throw new ScenarioValidationException("Copilot returned JSON null instead of a match.");
        }
        catch (JsonException e)
        {
            throw new ScenarioValidationException("Copilot returned malformed scenario JSON. The previous match was kept.", e);
        }
    }
}
