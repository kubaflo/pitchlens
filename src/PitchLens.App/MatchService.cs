using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.App;

public sealed record NativeView(PresentationFrame Frame, string? Model, bool Fresh, string Status, bool Generating, ScenarioOrigin Origin);
public sealed record EarlierGenerationIssue(GenerationState State,string Message,double ElapsedSeconds);
public enum SessionMode { Unchosen, Live, Test }

public sealed partial class MatchService : IDisposable
{
    private readonly object gate = new();
    private readonly ReplayPresentation presentation = new(DenseNarrationRecording.Create(), new MonotonicClock());
    private readonly GenerationService generation;
    private readonly IMonotonicClock filmClock = new MonotonicClock();
    private readonly Func<IProgress<string>, CancellationToken, Task> checkConnection;
    public RequestActivity GenerationActivity { get; } = new();
    public RequestActivity ConnectionActivity { get; } = new();
    private long generationActivityId, connectionActivityId;
    public MomentExplanationService Explainer { get; }
    public DirectorService Director { get; }
    private FilmPlayback? film;
    private Task? directorRequest;
    public bool FilmActive => film is not null;
    public bool FilmPlaying => film?.Playing == true;
    public FilmFrame? FilmFrame => film?.Frame;
    public string DirectorNotice { get; private set; } = "";
    public bool CanDirect => HasMatch && !disposed && !Generating && !CheckingConnection && !Explainer.Busy && !FilmActive &&
        (Director.Busy || DirectorEvidence.CanCapture(presentation.Match, presentation.Position));
    public MomentEvidence? ExplanationSnapshot { get; private set; }
    private CancellationTokenSource? cancellation;
    private CancellationTokenSource? connectionCancellation;
    private bool disposed;
    private bool startupAdmitted;
    public bool HasMatch { get; private set; }
    public SessionMode Mode { get; private set; }=SessionMode.Live;
    private readonly Action<SessionMode>? saveMode;
    private readonly Action<PitchViewSettings>? saveViewSettings;
    public PitchViewSettings ViewSettings{get;private set;}=PitchViewSettings.Default;
    public string? ViewSettingsNotice{get;private set;}
    private Task? momentRequest;
    public string? MomentIntentNotice { get; private set; }
    public InsightPreferences InsightPreferences { get; private set; }=InsightPreferences.Default;
    public string? InsightPreferenceNotice { get; private set; }
    public void SetInsightPreferences(InsightPreferences preferences)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(Explainer.Busy)throw new InvalidOperationException("Finish or cancel the captured request before changing insight preferences.");
            preferences.Validate(presentation.Match);InsightPreferences=preferences;InsightPreferenceNotice=null;
        }
        NotifyChanged();
    }
    public bool CanExplainCurrent
    {
        get { lock (gate) return HasMatch && !disposed && !Generating && !CheckingConnection && !Director.Busy && !FilmActive &&
            (Mode==SessionMode.Test?presentation.Position>=86&&MomentRecording.Available(presentation.Match,presentation.Revision):
            Explainer.Busy || presentation.Match.Events.Any(e => e.Time <= presentation.Position)); }
    }
    public event Action? Changed;
    public bool Active { get; private set; } = true;
    public string CliOverride { get; set; } = "";
    public string Status { get; private set; } = "Recorded AI-authored 11v11 Sol example · Not a fresh request";
    public bool Generating => cancellation is not null;
    public bool CheckingConnection => connectionCancellation is not null;
    public string ConnectionStatus { get; private set; } = "Connection not checked. This check makes no generation request.";
    public string? CurrentModel { get; private set; } = AuthoredMatchRecording.Model;
    public ScenarioOrigin Origin { get; private set; } = ScenarioOrigin.RecordedSdkExample;
    public bool Fresh { get; private set; }
    public GenerationState RequestState => generation.State;
    public EarlierGenerationIssue? PriorGenerationIssue { get; private set; }
    public bool RecordedDemoAfterGenerationIssue { get; private set; }
    public Match Match => presentation.Match;
    public MatchFixture? RequestedFixture => generation.RequestedFixture;
    public FixtureChoice FixtureChoice { get; private set; }=new();
    public bool FixturePickerOpen { get; private set; }
    private Task? submittedGeneration;
    public void OpenFixturePicker()
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(Mode!=SessionMode.Live)throw new InvalidOperationException("Team selection is for Live AI matches. Test keeps its recorded Arsenal v Manchester City fixture.");
            if(Generating||CheckingConnection||Explainer.Busy||Director.Busy||FilmActive)
                throw new InvalidOperationException("Finish or cancel the current request or film before choosing teams.");
            FixturePickerOpen=true;
        }
        NotifyChanged();
    }
    public void SetFixtureChoice(FixtureChoice value)
    {
        lock(gate)
        {
            if(Mode!=SessionMode.Live||!FixturePickerOpen||Generating)throw new InvalidOperationException("Open the idle Live fixture picker before changing teams.");
            _=value.Capture();FixtureChoice=value;
        }
        NotifyChanged();
    }
    public void CancelFixturePicker(){lock(gate)FixturePickerOpen=false;NotifyChanged();}
    public Task SubmitFixtureAsync()
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(Mode!=SessionMode.Live)throw new InvalidOperationException("Test mode cannot submit an AI fixture.");
            if(Generating)return submittedGeneration??Task.CompletedTask;
            if(CheckingConnection||Explainer.Busy||Director.Busy||FilmActive)
                throw new InvalidOperationException("Finish or cancel the current request or film before creating a match.");
            if(!FixturePickerOpen)throw new InvalidOperationException("Choose teams before creating a match.");
            var captured=FixtureChoice.Capture();
            FixturePickerOpen=false;
            return submittedGeneration=GenerateAsync(captured);
        }
    }

    public MatchService(IScenarioSource scenarios, IMomentSource moments, Func<IProgress<string>, CancellationToken, Task> connection,
        IDirectorSource? director = null, IMonotonicClock? filmClock = null, bool readyDemo = true, MatchVariety? variety = null,
        SessionMode mode=SessionMode.Live,Action<SessionMode>? saveMode=null,IMonotonicClock? replayClock=null,
        PitchViewSettings? viewSettings=null,Action<PitchViewSettings>? saveViewSettings=null,string? viewSettingsNotice=null)
    {
        if(replayClock is not null)presentation=new(DenseNarrationRecording.Create(),replayClock);
        generation = new(scenarios, presentation.Match, ScenarioOrigin.RecordedSdkExample, AuthoredMatchRecording.Model, variety);
        HasMatch = readyDemo;
        Mode=mode;this.saveMode=saveMode;
        ViewSettings=viewSettings??PitchViewSettings.Default;ViewSettings.Validate();
        this.saveViewSettings=saveViewSettings;ViewSettingsNotice=viewSettingsNotice;
        Explainer = new(moments);
        Director = new(director ?? new UnavailableDirectorSource());
        if (filmClock is not null) this.filmClock = filmClock;
        checkConnection = connection;
        Initialize();
    }
    private void Initialize()
    {
        if (!HasMatch) { Status = "Choose teams before creating a Live AI match. No request has started."; CurrentModel = null; }
        Explainer.ChangeScene(presentation.Revision);
        Explainer.Changed += NotifyChanged;
        Director.ChangeScene(presentation.Revision);
        Director.Changed += NotifyChanged;
        presentation.Select(presentation.Match.Players.First(p=>p.Role=="midfielder").Id);
    }
    private void NotifyChanged() { if (!disposed) Changed?.Invoke(); }
    public void SetViewSettings(PitchViewSettings value)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);value.Validate();
            if(value==ViewSettings&&ViewSettingsNotice is null)return;
            saveViewSettings?.Invoke(value); // Failed persistence leaves the old settings intact; the UI reports the error.
            ViewSettings=value;ViewSettingsNotice=null;
        }
        NotifyChanged();
    }
    public Task StartSessionAsync()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (startupAdmitted) return submittedGeneration ?? Task.CompletedTask;
            startupAdmitted = true;
            if(Mode==SessionMode.Unchosen)return Task.CompletedTask;
            if(Mode==SessionMode.Test){PlayRecordedDemo();return Task.CompletedTask;}
            if (HasMatch) return Task.CompletedTask;
            FixturePickerOpen=true;Status="Choose teams, then Create match. No AI request has started.";
            return Task.CompletedTask;
        }
    }
    public Task ChooseModeAsync(SessionMode mode,bool openPicker=true)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(mode is not(SessionMode.Live or SessionMode.Test))throw new ArgumentException("Choose Live or Test.");
            if(GuidedDemoActive)throw new InvalidOperationException("Exit the guided demo before changing modes.");
            if(mode==SessionMode.Live&&Mode==mode&&Generating)return submittedGeneration??Task.CompletedTask;
            if(Generating||Explainer.Busy||Director.Busy||CheckingConnection)throw new InvalidOperationException("Cancel or finish active requests before changing mode.");
            saveMode?.Invoke(mode);Mode=mode;startupAdmitted=true;
            if(mode==SessionMode.Test)
            {
                FixturePickerOpen=false;
                presentation.Pause();
                if(!HasMatch)PlayRecordedDemo();
                Status=Fresh?"Test mode · Current fresh match retained; all SDK requests blocked.":"Test mode · Recorded SDK demo / Not fresh / No AI calls.";
            }
            else if(openPicker)FixturePickerOpen=true;
        }
        NotifyChanged();
        return Task.CompletedTask;
    }
    public void TestExplain()
    {
        lock(gate)
        {
            if(Mode!=SessionMode.Test||!MomentRecording.Available(presentation.Match,presentation.Revision)||presentation.Position<86)
                throw new InvalidOperationException("Recorded analysis is available only for the matching observed demo goal.");
            LoadRecordedMoment();ExplanationSnapshot=Explainer.Current!.Evidence;presentation.Pause();
        }
        NotifyChanged();
    }
    public void PlayRecordedDemo()
    {
        lock (gate)
        {
            if (disposed || Generating) throw new InvalidOperationException("Cancel or finish creation before selecting the recorded demo.");
            if (HasMatch && Fresh) throw new InvalidOperationException("The fresh match is retained; recorded demo is only available before first delivery.");
            startupAdmitted = true; HasMatch = true; Fresh = false; Origin = ScenarioOrigin.RecordedSdkExample;
            FixturePickerOpen=false;
            CurrentModel = AuthoredMatchRecording.Model;
            Status = "Explicit recorded SDK demo / Not fresh / No AI request.";
            if(generation.State is GenerationState.Failed or GenerationState.Cancelled && generation.TerminalIssue is { } issue)
            {
                if(!RecordedDemoAfterGenerationIssue)
                    PriorGenerationIssue=new(generation.State,issue,GenerationActivity.Snapshot.ElapsedSeconds);
                RecordedDemoAfterGenerationIssue=true;
                GenerationActivity.Reset();
            }
        }
        NotifyChanged();
    }

    public NativeView? Sample(bool force = false)
    {
        lock (gate)
        {
            var frame = disposed ? null : presentation.Sample(force);
            var sampledFilm=film?.Frame;
            if (frame is not null && sampledFilm is not null)
            {
                var sourceTime = sampledFilm.SourceTime;
                frame = frame with { Snapshot = Replay.At(presentation.Match, sourceTime), Playing = false,
                    Explanations = Explanations.At(presentation.Match, sourceTime, frame.Mode),
                    Story = StoryReplay.At(presentation.Match, sourceTime), EffectiveRate = 1, HighlightActive = false, HighlightsEnabled = false };
            }
            return frame is null ? null : new(frame, CurrentModel, Fresh, Status, Generating, Origin);
        }
    }
    public void Command(string command, double value = 0, string? id = null)
    {
        lock (gate)
        {
            if (disposed) return;
            if (!HasMatch && command != "pause") return;
            if (film is not null) { DirectorNotice = "Exit Director's Cut to use the match replay and player controls."; return; }
            switch (command)
            {
                case "play": if (Active) presentation.Play(); break;
                case "pause": presentation.Pause(); break;
                case "restart": presentation.Restart(); break;
                case "toggle": if (Active) presentation.Toggle(); break;
                case "rate": presentation.SetRate(value); break;
                case "highlights": presentation.SetHighlights(value != 0); break;
                case "watch-goal": if (Active) presentation.WatchGoal(id ?? throw new ArgumentException("Goal ID required.")); break;
                case "seek": presentation.Seek(value); break;
                case "seek-by": presentation.Seek(presentation.Position + value); break;
                case "select": presentation.Select(id ?? throw new ArgumentException("Player ID required.")); break;
                case "mode":
                    presentation.SetMode(value switch
                    {
                        0 => ExplanationMode.Casual,
                        1 => ExplanationMode.Analyst,
                        _ => throw new ArgumentOutOfRangeException(nameof(value))
                    });
                    break;
                default: throw new ArgumentException("Unknown replay command.");
            }
        }
        Changed?.Invoke();
    }
    public void Activate() { lock (gate) { if (!disposed) Active = true; } }
    public void Suspend()
    {
        lock (gate)
        {
            if (disposed) return;
            presentation.Pause();
            film?.Pause();
            Active = false;
        }
        Changed?.Invoke();
    }
    public void Cancel()
    {
        lock (gate)
        {
            if (cancellation is not null) GenerationActivity.Report(generationActivityId, new(RequestStage.Cancelling, "Cancelling match creation; waiting for cleanup…"));
            cancellation?.Cancel();
        }
    }
    public void CancelConnection() { lock (gate) connectionCancellation?.Cancel(); }
    public void CaptureMoment(string? eventId = null)
    {
        lock (gate)
        {
            if (disposed || !HasMatch || Explainer.Busy) return;
            presentation.Pause();
            ExplanationSnapshot = MomentEvidence.Capture(presentation.Match, presentation.Revision, presentation.Position, eventId,preferences:InsightPreferences);
        }
        Changed?.Invoke();
    }
    public void InspectEvidence(MomentEvidence captured, string id)
    {
        lock (gate)
        {
            if (disposed || presentation.Revision != captured.Revision)
                throw new InvalidOperationException("This evidence belongs to an earlier match. The current replay was not changed.");
            film = null;
            var fact = captured.Resolve(id);
            presentation.Pause();
            presentation.Seek(fact.Time);
        }
        Changed?.Invoke();
    }
    public MomentEvidence ConnectionEvidence(PitchLensView captured,string edgeId)
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(FilmActive||captured.Window.Revision!=presentation.Revision||
                captured.Window.Fixture!=ObservedWindow.FixtureKey(presentation.Match)||
                captured.Window.Cutoff>presentation.Position)
                throw new InvalidOperationException("This connection is from an earlier match or a later cutoff. Refresh the lens before replaying evidence.");
            var recomputed=MatchLenses.Connections(presentation.Match,captured.Window,captured.SubjectId);
            var edge=recomputed.Edges.SingleOrDefault(e=>e.Id==edgeId)
                ??throw new InvalidOperationException("This connection is no longer present in the observed window.");
            var id=edge.EventIds[^1];
            var time=presentation.Match.Events.Single(e=>e.Id==id).Time;
            return MomentEvidence.Capture(presentation.Match,presentation.Revision,time,id,[id]);
        }
    }
    public async Task ExplainAsync()
    {
        MomentEvidence captured;
        lock (gate)
        {
            if (Mode!=SessionMode.Live)throw new InvalidOperationException("Test mode blocks AI requests. Switch to Live explicitly.");
            if (disposed || Generating || CheckingConnection || Explainer.Busy || Director.Busy || FilmActive) return;
            captured = ExplanationSnapshot ?? throw new InvalidOperationException("Capture an observed moment first.");
        }
        await Explainer.GenerateAsync(captured, CancellationToken.None).ConfigureAwait(false);
    }
    public Task ExplainCurrentMomentAsync(bool retry = false)
    {
        Task pending;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            MomentIntentNotice = null;
            if(Mode!=SessionMode.Live){MomentIntentNotice="Test mode blocks AI analysis. Use the labelled recorded analysis or switch to Live.";return Task.CompletedTask;}
            if (Generating || CheckingConnection || Director.Busy || FilmActive)
            {
                MomentIntentNotice = Director.Busy || FilmActive ? "Director's Cut is active. Finish or exit it before moment analysis." :
                    Generating ? "Match creation is running. Moment analysis is unavailable until it finishes." :
                    "SDK connection check is running. Moment analysis is unavailable until it finishes.";
                pending = Task.CompletedTask;
            }
            else if (Explainer.Busy)
            {
                presentation.Pause();
                pending = momentRequest ?? Task.CompletedTask;
            }
            else if (!presentation.Match.Events.Any(e => e.Time <= presentation.Position))
            {
                MomentIntentNotice = "No observed event yet. Play to an event before requesting analysis.";
                pending = Task.CompletedTask;
            }
            else
            {
                presentation.Pause();
                var captured = MomentEvidence.Capture(presentation.Match, presentation.Revision, presentation.Position,preferences:InsightPreferences);
                ExplanationSnapshot = captured;
                if(!captured.HasRequestedContribution)
                {
                    MomentIntentNotice="No observed involvement for that player in this captured event window. No AI request sent; choose another focus or task.";
                    NotifyChanged();return Task.CompletedTask;
                }
                var failedSameSnapshot = Explainer.LastRequestedHash == Explainer.RequestKey(captured) &&
                    Explainer.Activity.Snapshot.Stage is RequestStage.Failed or RequestStage.Cancelled;
                if (!retry && failedSameSnapshot)
                    pending = Task.CompletedTask;
                else if (!retry && Explainer.TryReuseFresh(captured))
                    pending = Task.CompletedTask;
                else
                    pending = momentRequest = Explainer.GenerateAsync(captured, CancellationToken.None);
            }
        }
        NotifyChanged();
        return pending;
    }
    public void LoadRecordedMoment()
    {
        lock (gate)
        {
            if (disposed) return;
            Explainer.UseRecorded(MomentRecording.Load(presentation.Match, presentation.Revision, presentation.Position));
        }
        Changed?.Invoke();
    }
    public async Task CheckConnectionAsync()
    {
        using var token = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        lock (gate)
        {
            if(Mode!=SessionMode.Live){ConnectionStatus="Test mode · SDK connection checks are blocked.";NotifyChanged();return;}
            if (disposed || Generating || CheckingConnection || Explainer.Busy || Director.Busy || FilmActive) return;
            connectionCancellation = token;
            ConnectionStatus = "Checking bundled runtime, normal login and exact Sol availability. No AI generation.";
            connectionActivityId = ConnectionActivity.Begin(ConnectionStatus);
        }
        Changed?.Invoke();
        var activityId = connectionActivityId;
        var progress = new InlineProgress<string>(message =>
        {
            lock (gate) { if (disposed || connectionCancellation != token) return; ConnectionStatus = message; }
            ConnectionActivity.Report(activityId, RequestProgress.FromSdkStatus(message));
        });
        try
        {
            await checkConnection(progress, token.Token).ConfigureAwait(false);
            lock (gate) ConnectionStatus = "Connection verified: runtime startup, normal authentication, gpt-6.1-sol enabled and empty session creation. No generation request made.";
        }
        catch (OperationCanceledException) { lock (gate) ConnectionStatus = "Connection check cancelled or exceeded 45 seconds. Previous match unchanged."; }
        catch (CopilotGenerationException e) { lock (gate) ConnectionStatus = $"{e.Message} {e.Diagnostic}"; }
        finally
        {
            lock (gate) { connectionCancellation = null; MomentIntentNotice = null; }
            ConnectionActivity.Finish(activityId, ConnectionStatus.StartsWith("Connection verified", StringComparison.Ordinal) ? RequestStage.Complete :
                token.IsCancellationRequested ? RequestStage.Cancelled : RequestStage.Failed, ConnectionStatus);
            if (!disposed) Changed?.Invoke();
        }
    }
    public Task GenerateAsync()=>GenerateAsync(null);
    public async Task GenerateAsync(MatchFixture? selectedFixture)
    {
        CancellationTokenSource token;
        lock (gate)
        {
            if(Mode!=SessionMode.Live){Status="Test mode · New AI match is disabled. Switch to Live explicitly.";NotifyChanged();return;}
            if (disposed || cancellation is not null || CheckingConnection || Explainer.Busy || Director.Busy || FilmActive) return;
            if(selectedFixture is not null)selectedFixture=FixtureCatalog.RequireKnown(selectedFixture);
            token=new CancellationTokenSource();
            cancellation = token;
            RecordedDemoAfterGenerationIssue=false;
            generationActivityId = GenerationActivity.Begin("Preparing the match-generation request…");
        }
        Command("pause");
        var activityId = generationActivityId;
        string? shownFixture=null;
        var progress = new InlineProgress<string>(message =>
        {
            var fixtureChanged=false;
            lock (gate)
            {
                if (disposed || cancellation != token || generation.State != GenerationState.Generating) return;
                Status = message;
                if(generation.RequestedFixture is { } selected && selected.Id!=shownFixture){shownFixture=selected.Id;fixtureChanged=true;}
            }
            GenerationActivity.Report(activityId, RequestProgress.FromSdkStatus(message));
            if(fixtureChanged) NotifyChanged();
        });
        try
        {
            await generation.GenerateAsync(progress, token.Token,selectedFixture).ConfigureAwait(false);
            lock (gate)
            {
                if (disposed) return;
                Status = generation.Status;
                if (generation.State == GenerationState.Succeeded)
                {
                    presentation.Replace(generation.Current);
                    try{InsightPreferences.Validate(presentation.Match);}
                    catch(ArgumentException){InsightPreferences=InsightPreferences.Default;InsightPreferenceNotice="New fixture: the previous insight focus is unavailable. Default task and both-club focus restored.";}
                    HasMatch = true;
                    Explainer.ChangeScene(presentation.Revision);
                    ExplanationSnapshot = null;
                    MomentIntentNotice = null;
                    momentRequest = null;
                    film = null; Director.ChangeScene(presentation.Revision); directorRequest = null;
                    CurrentModel = generation.CurrentModel;
                    Fresh = true;
                    Origin = generation.Origin;
                }
                else if (!HasMatch) Status = generation.Status.Replace("Previous match kept.", "No active match. Try again explicitly or choose the recorded demo.", StringComparison.Ordinal);
            }
        }
        finally
        {
            lock (gate)
            {
                cancellation = null;
                MomentIntentNotice = null;
                token.Dispose();
                GenerationActivity.Finish(activityId, generation.State == GenerationState.Succeeded ? RequestStage.Complete :
                    generation.State == GenerationState.Cancelled ? RequestStage.Cancelled : RequestStage.Failed, Status);
            }
            if (!disposed) Changed?.Invoke();
        }
    }
    public void Freeze(double time, long revision)
    {
        lock (gate)
        {
            if (disposed || presentation.Revision != revision) return;
            if (film is not null) film.Pause();
            else
            {
                if (!Active) time = Math.Min(time, presentation.Position);
                presentation.Pause();
                presentation.Seek(time);
            }
        }
        Changed?.Invoke();
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            cancellation?.Cancel();
            connectionCancellation?.Cancel();
            Explainer.Cancel();
            Explainer.Changed -= NotifyChanged;
            Director.Cancel(); Director.Changed -= NotifyChanged;
            presentation.Dispose();
            disposed = true;
        }
    }
    public Task DirectCurrentAsync(bool retry = false)
    {
        Task pending;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if(Mode!=SessionMode.Live){DirectorNotice="Test mode blocks AI editing. Use the labelled recorded film preview or switch to Live.";return Task.CompletedTask;}
            if (Director.Busy) return directorRequest ?? Task.CompletedTask;
            if (!CanDirect) { DirectorNotice = "Observe a goal and at least a minute of the match. Finish other requests and exit any film first."; return Task.CompletedTask; }
            presentation.Pause();
            var evidence = DirectorEvidence.Capture(presentation.Match, presentation.Revision, presentation.Position);
            DirectorNotice = $"Captured through {evidence.Cutoff:0.0}s. Main match stays paused; return restores this position, speed and selection.";
            pending = directorRequest = Director.CreateAsync(evidence, retry);
        }
        NotifyChanged();
        return pending;
    }
    public void WatchDirector()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (Director.Busy || Explainer.Busy || Generating || CheckingConnection)
                throw new InvalidOperationException("Finish the active request before watching a film.");
            var current = Director.Current ?? throw new InvalidOperationException("Create a valid film first.");
            if (current.Evidence.Revision != presentation.Revision) throw new InvalidOperationException("Film belongs to a previous match.");
            presentation.Pause(); film = new(current.Plan, filmClock); film.Play();
        }
        NotifyChanged();
    }
    public void PreviewRecordedDirector()
    {
        lock (gate)
        {
            if (Director.Busy || Explainer.Busy || Generating || CheckingConnection || FilmActive)
                throw new InvalidOperationException("Finish the current request or film before recorded preview.");
            presentation.Pause();
            Director.UseRecorded(DirectorRecording.Load(presentation.Match, presentation.Revision, presentation.Position));
        }
        WatchDirector();
    }
    public void FilmCommand(string command, double value = 0)
    {
        lock (gate)
        {
            if (film is null) return;
            switch (command)
            {
                case "toggle": if (film.Playing) film.Pause(); else if (Active) film.Play(); break;
                case "seek": film.Seek(value); break;
                case "restart": film.Restart(); if (!Active) film.Pause(); break;
                case "exit": film = null; break;
                default: throw new ArgumentException("Unknown film control.");
            }
        }
        NotifyChanged();
    }
    public (MomentEvidence Evidence, string EventId) CurrentFilmEvidence()
    {
        lock (gate)
        {
            var frame = film?.Frame;
            if (film is null || frame is null || !frame.Holding || Director.Current is null)
                throw new InvalidOperationException("Scene evidence is available at its freeze frame, after the recorded actions complete.");
            var scene = film.Plan.Scenes[frame.SceneIndex];
            var evidence = MomentEvidence.Capture(presentation.Match, presentation.Revision, scene.SourceEnd, scene.FocusEventId, scene.EvidenceIds);
            return (evidence, scene.FocusEventId);
        }
    }
    private sealed class UnavailableDirectorSource : IDirectorSource
    {
        public Task<string> CreateAsync(DirectorEvidence evidence, IProgress<string>? progress, CancellationToken token) =>
            throw new CopilotGenerationException("No Director SDK source configured.");
    }
}
