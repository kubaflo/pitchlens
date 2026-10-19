namespace PitchLens.Core;

public sealed record PresentationFrame(long Revision, long Sequence, Match Match, Snapshot Snapshot, bool Playing,
    string SelectedId, ExplanationMode Mode, IReadOnlyList<Explanation> Explanations, IReadOnlyList<StoryMoment> Story, double Rate);

public sealed class ReplayPresentation : IDisposable
{
    private readonly IMonotonicClock clock;
    private Playback playback;
    private double lastTime = double.NaN;
    private double lastSampleClock = double.NaN;
    private int eventCount = -1, storyCount = -1;
    private ExplanationMode cachedMode;
    private IReadOnlyList<Explanation> explanations = [];
    private IReadOnlyList<StoryMoment> story = [];
    private Snapshot? cachedSnapshot;
    private bool disposed;
    private long sequence;
    public Match Match { get; private set; }
    public string SelectedId { get; private set; }
    public ExplanationMode Mode { get; private set; }
    public long Revision { get; private set; } = 1;
    public int SnapshotComputations { get; private set; }
    public int ExplanationComputations { get; private set; }
    public int StoryComputations { get; private set; }
    public bool Playing => playback.IsPlaying;
    public double Position => playback.Position;
    public double Rate => playback.Rate;

    public ReplayPresentation(Match match, IMonotonicClock clock)
    {
        Match = match;
        this.clock = clock;
        playback = new(match.Duration, clock);
        SelectedId = match.Players[0].Id;
    }

    public PresentationFrame? Sample(bool force = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var time = playback.Position;
        var now = clock.Seconds;
        if (!force && double.IsFinite(lastTime) && time >= lastTime &&
            Math.Floor(now * 4) == Math.Floor(lastSampleClock * 4)) return null;
        lastTime = time;
        lastSampleClock = now;
        var snapshot = cachedSnapshot;
        if (snapshot is null || snapshot.Time != time)
        {
            snapshot = Replay.At(Match, time);
            cachedSnapshot = snapshot;
            SnapshotComputations++;
        }
        if (snapshot.Events.Count != eventCount || Mode != cachedMode)
        {
            explanations = Explanations.At(Match, time, Mode);
            ExplanationComputations++;
            eventCount = snapshot.Events.Count;
            cachedMode = Mode;
        }
        var beats = Match.Story?.Beats.Count(b => b.EndTime <= time) ?? 0;
        if (storyCount != beats)
        {
            story = StoryReplay.At(Match, time);
            StoryComputations++;
            storyCount = beats;
        }
        return new(Revision, ++sequence, Match, snapshot, playback.IsPlaying, SelectedId, Mode, explanations, story, playback.Rate);
    }

    public void Play() { ObjectDisposedException.ThrowIf(disposed, this); playback.Play(); }
    public void Pause() { ObjectDisposedException.ThrowIf(disposed, this); playback.Pause(); }
    public void Seek(double time) { ObjectDisposedException.ThrowIf(disposed, this); playback.Seek(time); }
    public void Restart() { ObjectDisposedException.ThrowIf(disposed, this); playback.Restart(); }
    public void SetRate(double rate) { ObjectDisposedException.ThrowIf(disposed, this); playback.SetRate(rate); }
    public void Toggle()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (playback.Position >= Match.Duration) { playback.Restart(); playback.Play(); }
        else if (playback.IsPlaying) playback.Pause();
        else playback.Play();
    }
    public void Select(string id)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        _ = Match.Player(id);
        SelectedId = id;
    }
    public void SetMode(ExplanationMode mode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
    }
    public void Replace(Match match)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        playback.Pause();
        Match = match;
        var rate = playback.Rate;
        playback = new(match.Duration, clock);
        playback.SetRate(rate);
        if (!match.Players.Any(p => p.Id == SelectedId)) SelectedId = match.Players[0].Id;
        Revision++;
        eventCount = storyCount = -1;
        cachedSnapshot = null;
        lastTime = double.NaN;
    }
    public void Dispose()
    {
        if (disposed) return;
        playback.Pause();
        disposed = true;
    }
}
