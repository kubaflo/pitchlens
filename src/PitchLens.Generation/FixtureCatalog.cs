using PitchLens.Core;

namespace PitchLens.Generation;

public sealed record MatchFixture(string Id, Team Home, Team Away, MatchRosterPlayer[] Players)
{
    public Team[] Teams => [Home, Away];
    public string PairKey => string.Join("|", new[] { Home.Id, Away.Id }.Order(StringComparer.Ordinal));
    public bool IsKeeper(string id) => Players.Single(p => p.Id == id).Role == "goalkeeper";
}

public static class FixtureCatalog
{
    public const string VerifiedOn = "2026-10-21";
    public static MatchFixture Historical => new("arsenal-v-manchester-city",
        new("arsenal","Arsenal","ARS","images/arsenal_crest.png","#00ff87"),
        new("manchester-city","Manchester City","MCI","images/manchester_city_crest.png","#ff83b8"),
        RegulationRoster.Players.ToArray());
    private static MatchRosterPlayer P(string club,string surname,string name,int number,string role) => new(club+"-"+surname,club,name,number,role);
    public static IReadOnlyList<Team> Clubs { get; } = new[]
    {
        Historical.Home, Historical.Away,
        new Team("liverpool","Liverpool","LIV","images/liverpool_crest.svg","#e32939"),
        new Team("chelsea","Chelsea","CHE","images/chelsea_crest.png","#5291ff"),
        new Team("manchester-united","Manchester United","MUN","images/manchester-united_crest.png","#ed554b"),
        new Team("tottenham-hotspur","Tottenham Hotspur","TOT","images/tottenham-hotspur_crest.png","#f5f4ec")
    };
    private static readonly MatchRosterPlayer[] Additional =
    [
        P("liverpool","alisson","Alisson Becker",1,"goalkeeper"),
        P("liverpool","frimpong","Jeremie Frimpong",30,"defender"),P("liverpool","van-dijk","Virgil van Dijk",4,"defender"),
        P("liverpool","gomez","Joe Gomez",2,"defender"),P("liverpool","kerkez","Milos Kerkez",6,"defender"),
        P("liverpool","gravenberch","Ryan Gravenberch",38,"midfielder"),P("liverpool","mac-allister","Alexis Mac Allister",10,"midfielder"),
        P("liverpool","szoboszlai","Dominik Szoboszlai",8,"midfielder"),P("liverpool","chiesa","Federico Chiesa",14,"forward"),
        P("liverpool","isak","Alexander Isak",9,"forward"),P("liverpool","gakpo","Cody Gakpo",18,"forward"),
        P("chelsea","martinez","Emiliano Martinez",1,"goalkeeper"),
        P("chelsea","james","Reece James",24,"defender"),P("chelsea","fofana","Wesley Fofana",3,"defender"),
        P("chelsea","colwill","Levi Colwill",6,"defender"),P("chelsea","hato","Jorrel Hato",21,"defender"),
        P("chelsea","caicedo","Moises Caicedo",25,"midfielder"),P("chelsea","lavia","Romeo Lavia",45,"midfielder"),
        P("chelsea","henderson","Jordan Henderson",14,"midfielder"),P("chelsea","palmer","Cole Palmer",10,"forward"),
        P("chelsea","pedro","Joao Pedro",9,"forward"),P("chelsea","neto","Pedro Neto",7,"forward"),
        P("manchester-united","lammens","Senne Lammens",1,"goalkeeper"),
        P("manchester-united","dalot","Diogo Dalot",2,"defender"),P("manchester-united","de-ligt","Matthijs de Ligt",4,"defender"),
        P("manchester-united","martinez","Lisandro Martínez",6,"defender"),P("manchester-united","shaw","Luke Shaw",23,"defender"),
        P("manchester-united","ugarte","Manuel Ugarte",25,"midfielder"),P("manchester-united","mainoo","Kobbie Mainoo",37,"midfielder"),
        P("manchester-united","fernandes","Bruno Fernandes",8,"midfielder"),P("manchester-united","amad","Amad",16,"forward"),
        P("manchester-united","sesko","Benjamin Šeško",30,"forward"),P("manchester-united","mbeumo","Bryan Mbeumo",19,"forward"),
        P("tottenham-hotspur","kinsky","Antonin Kinsky",31,"goalkeeper"),
        P("tottenham-hotspur","porro","Pedro Porro",23,"defender"),P("tottenham-hotspur","van-hecke","Jan Paul van Hecke",6,"defender"),
        P("tottenham-hotspur","van-de-ven","Micky van de Ven",37,"defender"),P("tottenham-hotspur","udogie","Destiny Udogie",13,"defender"),
        P("tottenham-hotspur","gallagher","Conor Gallagher",8,"midfielder"),P("tottenham-hotspur","gray","Archie Gray",14,"midfielder"),
        P("tottenham-hotspur","maddison","James Maddison",10,"midfielder"),P("tottenham-hotspur","kudus","Mohammed Kudus",20,"forward"),
        P("tottenham-hotspur","solanke","Dominic Solanke",19,"forward"),P("tottenham-hotspur","tel","Mathys Tel",11,"forward")
    ];
    public static MatchFixture Create(string home,string away)
    {
        if(home==away) throw new ArgumentException("Fixture requires different clubs.");
        var teams=new[]{Clubs.Single(t=>t.Id==home),Clubs.Single(t=>t.Id==away)};
        var players=RegulationRoster.Players.Concat(Additional).Where(p=>p.TeamId==home||p.TeamId==away)
            .OrderBy(p=>p.TeamId==home?0:1).ToArray();
        return new(home+"-v-"+away,teams[0],teams[1],players);
    }
    public static MatchFixture RequireKnown(MatchFixture supplied)
    {
        if(supplied.Home is null || supplied.Away is null || supplied.Players is not { Length:22 } || supplied.Players.Any(p=>p is null) ||
            !Clubs.Any(t=>t.Id==supplied.Home.Id) || !Clubs.Any(t=>t.Id==supplied.Away.Id) || supplied.Home.Id==supplied.Away.Id)
            throw new ScenarioValidationException("Fixture metadata requires two supported clubs and their exact22verified selected players.");
        var known=Create(supplied.Home.Id,supplied.Away.Id);
        if(known.Id!=supplied.Id || known.Home!=supplied.Home || known.Away!=supplied.Away ||
            !known.Players.SequenceEqual(supplied.Players)) throw new ScenarioValidationException("Fixture/roster metadata differs from the verified captured catalog.");
        return known;
    }
    public static MatchFixture Select(string? previousPair, int ordinal)
    {
        var pool=previousPair is null?Clubs.Skip(2):Clubs;
        var pairs=(from home in pool from away in pool where home.Id!=away.Id select Create(home.Id,away.Id)).ToArray();
        var choices=pairs.Where(f=>f.PairKey!=previousPair).ToArray();
        return choices[Math.Abs(ordinal%choices.Length)];
    }
}
