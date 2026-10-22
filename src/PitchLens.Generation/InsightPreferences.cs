using PitchLens.Core;

namespace PitchLens.Generation;

public enum InsightTask { WhyThisMatters, ExplainBuildUp, PlayerContribution }
public enum InsightScope { All, Club, Player }
public sealed record InsightPreferences(InsightTask Task=InsightTask.WhyThisMatters,InsightScope Scope=InsightScope.All,string? SubjectId=null)
{
    public static InsightPreferences Default { get; }=new();
    public void Validate(Match match)
    {
        if(!Enum.IsDefined(Task)||!Enum.IsDefined(Scope)||
            !(Scope switch { InsightScope.All=>SubjectId is null,InsightScope.Club=>match.Teams.Any(t=>t.Id==SubjectId),
                InsightScope.Player=>match.Players.Any(p=>p.Id==SubjectId),_=>false })||
            Task==InsightTask.PlayerContribution&&Scope!=InsightScope.Player)
            throw new ArgumentException("Choose a known insight task and match focus; player contribution requires a player.");
    }
    public string Label=>Task switch { InsightTask.WhyThisMatters=>"Why this matters",InsightTask.ExplainBuildUp=>"Explain the build-up",_=>"Player contribution" };
}
public sealed record MomentContext(InsightPreferences Preferences,string? SubjectName,string[] InvolvementIds,Dictionary<string,int> Score);
public sealed record MomentReadAudit(string Tool,string? EventId,double Cutoff,int Sequence);
