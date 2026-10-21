namespace PitchLens.Core;

public enum ObservedRange { Last60Seconds, MatchSoFar }
public enum PitchLensMode { Off, Connections, Movement }
public enum StoryFocusKind { All, Club, Player }
public sealed record StoryFocus(StoryFocusKind Kind=StoryFocusKind.All,string? Id=null);
public sealed record FocusedChapter(NarrativeChapter Chapter,bool GoalContext);
public sealed record LensOptions(PitchLensMode Mode=PitchLensMode.Off,ObservedRange Range=ObservedRange.Last60Seconds,
    string? ClubId=null,string? EdgeId=null,int? Cell=null);

public sealed record ObservedWindow(long Revision,string Fixture,double Start,double Cutoff)
{
    public double Duration=>Cutoff-Start;
    public static string FixtureKey(Match match)=>string.Join("/",match.Teams.Select(t=>t.Id));
    public static ObservedWindow Capture(Match match,long revision,double cutoff,ObservedRange range)
    {
        if(revision<1||!Enum.IsDefined(range)||!double.IsFinite(cutoff)||cutoff<0||cutoff>match.Duration)
            throw new ArgumentOutOfRangeException(nameof(cutoff),"A valid observed replay cutoff and revision are required.");
        return new(revision,FixtureKey(match),range==ObservedRange.Last60Seconds?Math.Max(0,cutoff-60):0,cutoff);
    }
    public void Validate(Match match)
    {
        if(Revision<1||Fixture!=FixtureKey(match)||!double.IsFinite(Start)||!double.IsFinite(Cutoff)||
            Start<0||Cutoff<Start||Cutoff>match.Duration)throw new ArgumentException("Observed window does not belong to this fixture or valid source time.");
    }
    // Completion-in-window, inclusive at both ends; source paths can start before the window.
    public IEnumerable<MatchEvent> Events(Match match)
    {
        Validate(match);
        return match.Events.Where(e=>e.Time>=Start&&e.Time<=Cutoff);
    }
}
public sealed record ConnectionNode(string PlayerId,double X,double Y,int Samples);
public sealed record ConnectionEdge(string Id,string From,string To,int Count,string[] EventIds,double[] PassMetres);
public sealed record MovementCell(int Index,int Column,int Row,double X,double Y,double Width,double Height,double Seconds);
public sealed record PitchLensView(ObservedWindow Window,string Mode,string SubjectId,ConnectionNode[] Nodes,
    ConnectionEdge[] Edges,MovementCell[] Cells,int CompletedPasses,int InterceptedAttempts,double ObservedSeconds,
    double? DistanceMetres,string? SelectedEdgeId=null,int? SelectedCell=null);

public static class MatchLenses
{
    public const int Columns=7,Rows=4;
    public static PitchLensView Connections(Match match,ObservedWindow window,string clubId,string? selectedEdgeId=null)
    {
        window.Validate(match);
        if(!match.Teams.Any(t=>t.Id==clubId))throw new ArgumentException("Choose a club in this match.");
        var attempts=window.Events(match).Where(e=>e.Kind==EventKind.Pass&&match.Player(e.ActorId).TeamId==clubId).ToArray();
        var passes=attempts.Where(e=>e.Completed==true&&match.Player(e.ReceiverId!).TeamId==clubId).ToArray();
        var samples=new Dictionary<string,List<Position>>();
        void Add(string id,Position point)
        {
            if(!samples.TryGetValue(id,out var positions))samples[id]=positions=[];
            positions.Add(point);
        }
        foreach(var pass in passes)
        {
            Add(pass.ActorId,match.Ball.At(pass.StartTime!.Value));
            Add(pass.ReceiverId!,match.Ball.At(pass.Time));
        }
        var nodes=samples.OrderBy(s=>s.Key,StringComparer.Ordinal).Select(s=>new ConnectionNode(s.Key,
            s.Value.Average(p=>p.X),s.Value.Average(p=>p.Y),s.Value.Count)).ToArray();
        var edges=passes.GroupBy(e=>(e.ActorId,e.ReceiverId)).OrderBy(g=>g.Key.ActorId,StringComparer.Ordinal)
            .ThenBy(g=>g.Key.ReceiverId,StringComparer.Ordinal).Select(g=>new ConnectionEdge(
                g.Key.ActorId+":to:"+g.Key.ReceiverId,g.Key.ActorId,g.Key.ReceiverId!,g.Count(),g.Select(e=>e.Id).ToArray(),
                g.Select(e=>match.Ball.Measure(e.Time).Distance-match.Ball.Measure(e.StartTime!.Value).Distance).ToArray())).ToArray();
        if(selectedEdgeId is not null&&!edges.Any(e=>e.Id==selectedEdgeId))selectedEdgeId=null;
        return new(window,"connections",clubId,nodes,edges,[],passes.Length,attempts.Length-passes.Length,
            window.Duration,null,selectedEdgeId);
    }
    public static PitchLensView Movement(Match match,ObservedWindow window,string playerId,int? selectedCell=null)
    {
        window.Validate(match);
        var player=match.Players.SingleOrDefault(p=>p.Id==playerId)??throw new ArgumentException("Choose a player in this match.");
        var cells=Occupancy(player.Path,window.Start,window.Cutoff);
        if(selectedCell is <0 or >=Columns*Rows)throw new ArgumentOutOfRangeException(nameof(selectedCell));
        return new(window,"movement",playerId,[],[],cells,0,0,window.Duration,
            window.Duration==0?null:player.Path.Measure(window.Cutoff).Distance-player.Path.Measure(window.Start).Distance,
            SelectedCell:selectedCell);
    }
    public static MovementCell[] Occupancy(Trajectory path,double start,double end)
    {
        if(!double.IsFinite(start)||!double.IsFinite(end)||start<0||end<start||
            path.Frames.Count<2||start<path.Frames[0].Time||end>path.Frames[^1].Time)
            throw new ArgumentOutOfRangeException(nameof(start),"Movement needs a finite clipped trajectory window.");
        var seconds=new double[Columns*Rows];
        const double width=Match.PitchWidth/Columns,height=Match.PitchHeight/Rows;
        for(var i=1;i<path.Frames.Count;i++)
        {
            var a=path.Frames[i-1];var b=path.Frames[i];
            if(!double.IsFinite(a.Time)||!double.IsFinite(b.Time)||b.Time<=a.Time||
                !Valid(a.Position)||!Valid(b.Position))throw new ArgumentException("Invalid movement trajectory.");
            var from=Math.Max(start,a.Time);var to=Math.Min(end,b.Time);
            if(to<=from)continue;
            var p=Position.Lerp(a.Position,b.Position,(from-a.Time)/(b.Time-a.Time));
            var q=Position.Lerp(a.Position,b.Position,(to-a.Time)/(b.Time-a.Time));
            var cuts=new List<double>{0,1};
            void Crossings(double origin,double delta,double size,int count)
            {
                if(delta==0)return;
                for(var line=1;line<count;line++)
                {
                    var fraction=(line*size-origin)/delta;
                    if(fraction>0&&fraction<1)cuts.Add(fraction);
                }
            }
            Crossings(p.X,q.X-p.X,width,Columns);Crossings(p.Y,q.Y-p.Y,height,Rows);
            var sorted=cuts.Distinct().Order().ToArray();
            for(var j=1;j<sorted.Length;j++)
            {
                var mid=Position.Lerp(p,q,(sorted[j-1]+sorted[j])/2);
                var column=Math.Clamp((int)(mid.X/width),0,Columns-1);
                var row=Math.Clamp((int)(mid.Y/height),0,Rows-1);
                seconds[row*Columns+column]+=(to-from)*(sorted[j]-sorted[j-1]);
            }
        }
        return seconds.Select((value,index)=>new MovementCell(index,index%Columns,index/Columns,
            index%Columns*width,index/Columns*height,width,height,value)).ToArray();
    }
    private static bool Valid(Position point)=>double.IsFinite(point.X)&&double.IsFinite(point.Y)&&
        point.X>=0&&point.X<=Match.PitchWidth&&point.Y>=0&&point.Y<=Match.PitchHeight;

    public static bool FocusAvailable(Match match,StoryFocus focus)=>focus.Kind switch
    {
        StoryFocusKind.All=>true,
        StoryFocusKind.Club=>match.Teams.Any(t=>t.Id==focus.Id),
        StoryFocusKind.Player=>match.Players.Any(p=>p.Id==focus.Id),
        _=>false
    };
    public static IReadOnlyList<FocusedChapter> Story(Match match,double cutoff,StoryFocus focus)
    {
        if(!FocusAvailable(match,focus))throw new ArgumentException("Story favorite is not in this match.");
        return NarrativeReplay.At(match,cutoff).Where(chapter=>chapter.Kind=="goal"||focus.Kind==StoryFocusKind.All||
            chapter.EventIds.SelectMany(id=>
            {
                var e=match.Events.Single(e=>e.Id==id);
                return e.ReceiverId is null?new[]{e.ActorId}:new[]{e.ActorId,e.ReceiverId};
            }).Any(id=>focus.Kind==StoryFocusKind.Player?id==focus.Id:match.Player(id).TeamId==focus.Id))
            .Select(chapter=>new FocusedChapter(chapter,focus.Kind!=StoryFocusKind.All&&chapter.Kind=="goal")).ToArray();
    }
}
