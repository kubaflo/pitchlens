namespace PitchLens.Generation;

public enum FixtureChoiceKind { Manual, Random }
public sealed record FixtureChoice(FixtureChoiceKind Kind=FixtureChoiceKind.Manual,string? HomeId="arsenal",string? AwayId="manchester-city")
{
    public static FixtureChoice Random { get; }=new(FixtureChoiceKind.Random,null,null);
    public MatchFixture? Capture()
    {
        if(!Enum.IsDefined(Kind))throw new ArgumentException("Choose a known fixture selection mode.");
        if(Kind==FixtureChoiceKind.Random)
        {
            if(HomeId is not null||AwayId is not null)throw new ArgumentException("Random matchup cannot contain manual club IDs.");
            return null;
        }
        if(!FixtureCatalog.Clubs.Any(t=>t.Id==HomeId)||!FixtureCatalog.Clubs.Any(t=>t.Id==AwayId))
            throw new ArgumentException("Choose two supported clubs.");
        if(HomeId==AwayId)throw new ArgumentException("Home and away clubs must be different.");
        return FixtureCatalog.Create(HomeId!,AwayId!);
    }
}
