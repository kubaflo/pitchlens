using System.ComponentModel;
using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace PitchLens.Generation;

// SDK 1.0.22 marks permission decisions and response schemas experimental.
#pragma warning disable GHCP001
public sealed class CopilotScenarioSource(string cliPath, string workingDirectory) : IScenarioSource
{
    public const string Model = "gpt-6.1-sol";

    public async Task<string> GenerateAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(cliPath) || !File.Exists(cliPath))
            throw new CopilotGenerationException("Copilot runtime executable is missing. Rebuild with bundled runtime acquisition enabled, or select a compatible installed CLI. Run copilot login in Terminal first.");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var client = new CopilotClient(new CopilotClientOptions
            {
                Connection = RuntimeConnection.ForStdio(cliPath),
                WorkingDirectory = workingDirectory,
                UseLoggedInUser = true,
                LogLevel = CopilotLogLevel.Error
            });
            await using var owner = new BoundedClient(client);
            progress?.Report("Starting local Copilot CLI…");
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            progress?.Report("Checking Copilot authentication…");
            var auth = await client.GetAuthStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!auth.IsAuthenticated)
                throw new CopilotGenerationException("Copilot is not authenticated. Run copilot login in Terminal with a Copilot-enabled account.");
            progress?.Report("Checking required GPT model availability…");
            var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);
            RequireModel(models);
            progress?.Report($"Generating one structured synthetic match with {Model}…");
            var session = await client.CreateSessionAsync(CreateSessionConfig(), cancellationToken).ConfigureAwait(false);
            try
            {
                using var schema = JsonDocument.Parse(Schema);
                var response = await session.SendAndWaitAsync(new MessageOptions
                {
                    Prompt = Prompt,
                    ResponseSchema = schema.RootElement.Clone()
                }, TimeSpan.FromMinutes(4.5), cancellationToken).ConfigureAwait(false);
                return response?.Data.Content ?? throw new CopilotGenerationException("Copilot returned no scenario response.");
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await session.AbortAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new CopilotGenerationException("Copilot session cleanup exceeded five seconds. The owned runtime will be disposed.", e);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotGenerationException) { throw; }
        catch (JsonException e)
        {
            throw new CopilotGenerationException("Copilot SDK/runtime protocol mismatch. Clear the CLI override to use the bundled 1.0.98 runtime, or install a compatible version.", e);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or Win32Exception or TimeoutException or RemoteRpcException or UnauthorizedAccessException or AggregateException)
        {
            throw new CopilotGenerationException($"Copilot SDK/CLI request failed ({e.GetType().Name}). Check CLI compatibility, copilot login, network and access to {Model}.", e);
        }
    }

    public static ModelInfo RequireModel(IEnumerable<ModelInfo> models)
    {
        var selected = models.SingleOrDefault(m => m.Id == Model);
        if (selected is null || selected.Policy?.State == "disabled")
            throw new CopilotGenerationException($"Required GPT model {Model} is unavailable or disabled for your Copilot account. No model fallback is allowed.");
        return selected;
    }

    public static SessionConfig CreateSessionConfig() => new()
    {
        Model = Model,
        ReasoningEffort = "medium",
        AvailableTools = [],
        OnPermissionRequest = (_, _) => Task.FromResult(PermissionDecision.Reject("Scenario generation does not permit tool access.")),
        EnableConfigDiscovery = false,
        EnableSkills = false,
        EnableFileHooks = false,
        EnableHostGitOperations = false,
        EnableSessionStore = false,
        EnableOnDemandInstructionDiscovery = false,
        EnableSessionTelemetry = false,
        InfiniteSessions = new InfiniteSessionConfig { Enabled = false },
        SystemMessage = new SystemMessageConfig
        {
            Mode = SystemMessageMode.Replace,
            Content = "You are a football replay choreographer. Plan the story first, then coordinate every player's purposeful movement with the causal event timeline. Output only the required structured JSON. Do not use tools, agents, files or external services. These actions are invented, not real athlete performance."
        }
    };

    private sealed class BoundedClient(CopilotClient client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Client disposal owns its sessions; their detach RPC otherwise has no deadline.
            var disposal = client.DisposeAsync().AsTask();
            try
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                await client.ForceStopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                await disposal.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                throw new CopilotGenerationException("Copilot cleanup timed out; its owned runtime was stopped. The match was not replaced.", e);
            }
        }
    }

    public static string Prompt => """
        Create an ORIGINAL engaging synthetic 72-second, reduced-sided Arsenal v Manchester City match.
        Use ALL 12 verified roster IDs exactly once. Names/clubs are factual 2026/27; EVERY action is invented.
        STORY FIRST: choose an arc of build-up under pressure, an intercepted pass/turnover, counterattack,
        a supporting/overlapping run to receive a pass, a convincing finish, then the other team's response.
        You choose substantive actors, event times, runs, score and complete trajectories. Not a seed or commentary.
        Plan the event/possession timeline internally BEFORE choosing coordinated keyframes. Then output only schema JSON.
        Story title must be one of Pressure and Space / The Turning Tide / A Game of Transitions / Seventy-Two Seconds
        (neutral titles avoid revealing future results). Include 6..9 beats with unique short ASCII IDs.
        Beat kind: build-up, turnover, counterattack, support-run, finish, response. Include ALL six kinds.
        First kind build-up; turnover before counterattack before finish before response.
        Beat ranges must be contiguous from exactly 0 to exactly 72 (no gaps/overlap).
        Every beat references >=1 known event IDs resolving inside its range, and 2..6 real player IDs.
        Include all referenced event actors/receivers in playerIds; include a named reacting defender too where needed.
        Every event must appear in a beat. Story claims are derived from these verified anchors, not free commentary.
        BOTH clubs must complete >=1 successful pass AND >=1 moving carry and gain possession.
        The turnover is an intercepted pass (completed=false, receiver on opposing team); do not invent a tackle event.
        Use 10..20 events if useful, prioritizing complete action/response over excess types.
        Player positions are metres x=0..105,y=0..68. Arsenal attacks RIGHT (105); City attacks LEFT (0).
        IMPORTANT keepers: Raya starts x<=10,y24..44 and stays x<=18,y18..50; Donnarumma starts
        x>=95,y24..44 and stays x>=87,y18..50. Never put both keepers at the same end.
        Outfield players need purposeful coordinated runs/recovery, changes of direction, not idle background/jitter.
        EACH club must have >=3 active outfield players with >=6 keyframes, >=35m total movement,
        a >=3m/s burst, and >=2 direction changes of >=32 degrees between segments of >=4m.
        Aim for 60..140m travel, varied 2..6m/s bursts, then meaningful recovery; do NOT jitter to meet thresholds.
        All segments must stay <=8m/s. Give most outfield players 10..16 keyframes, including action arrival times.
        Shared sensible time boundaries help. Each path begins at0, ends at72, has strictly increasing times.
        At every story boundary, each team's outfielders must span >=20m (at least two are separated by20m).
        Maintain width/depth and coordinate defensive reactions as possession changes; do not pile everyone on ball.
        Build-up AND counterattack beats each need a completed pass and a named OPPOSING defender in playerIds.
        That defender must move >=8m during the beat, CLOSE its distance to the ball by >=5m from beat start
        to beat end, and finish within15m of the ball. Choose endpoints deliberately to make this visible.
        Counterattack also needs a moving carry.
        Support-run beat MUST include a completed pass to an outfield runner who travels >=10m and advances
        >=8m toward their attacking goal between beat start and receiving time. Runner must be off-ball
        before receiving; actor still owns ball until flight. Ball will meet receiver's path at arrival.
        Finish beat references both shot AND goal. Response references an OPPOSING kickoff plus a completed pass.
        After the goal, show renewed supporting movement; do not freeze everyone. A second attack/goal is optional.
        Events strictly ordered by time, then order (0 normally; shot order0 and paired goal order1 at same time).
        kind=pass/carry/shot/goal/kickoff. Event IDs unique short ASCII.
        Each pass/carry/shot has startTime >= prior action END, duration>=1s, no overlapping actions.
        Actor must own ball. A pass transfers ownership to receiver at resolved time; completed must agree with teams.
        Pass requires receiverId and completed. All other kinds have receiverId=null,completed=null.
        Carries move>=1m along carrier path; ball automatically follows. All shots must have a paired same-time goal.
        A flight starts STRICTLY AFTER previous possession change and has <=40m/s ball speed to receiver
        (or attacking goal x=105/0,y=34). Model the shooter near the correct attacking goal.
        A goal requires an opposing kickoff >=2s later BEFORE further play. Kickoff actor must be at centre
        (52.5,34) at that instant; goal/kickoff startTime=null.
        Do a final internal check of speeds, beat endpoints, defender distances, runner movement and possession chain.
        Supply every field including nullable receiverId/completed/startTime. Verified roster:
        """ + JsonSerializer.Serialize(PremierLeagueRoster.Players.ToArray(), ScenarioJsonContext.Default.RosterPlayerArray);

    private const string Schema = """
        {"type":"object","additionalProperties":false,"required":["duration","initialOwnerId","players","events","story"],"properties":{
        "duration":{"type":"number","const":72},"initialOwnerId":{"type":"string"},
        "players":{"type":"array","minItems":12,"maxItems":12,"items":{"type":"object","additionalProperties":false,
          "required":["id","frames"],"properties":{"id":{"type":"string"},"frames":{"type":"array","minItems":2,"maxItems":24,
          "items":{"type":"object","additionalProperties":false,"required":["time","x","y"],"properties":{
          "time":{"type":"number"},"x":{"type":"number"},"y":{"type":"number"}}}}}}},
        "events":{"type":"array","minItems":5,"maxItems":20,"items":{"type":"object","additionalProperties":false,
          "required":["id","time","order","kind","actorId","receiverId","completed","startTime"],"properties":{
          "id":{"type":"string"},"time":{"type":"number"},"order":{"type":"integer"},"kind":{"type":"string","enum":["pass","carry","shot","goal","kickoff"]},
          "actorId":{"type":"string"},"receiverId":{"type":["string","null"]},"completed":{"type":["boolean","null"]},"startTime":{"type":["number","null"]}}}},
        "story":{"type":"object","additionalProperties":false,"required":["title","beats"],"properties":{
          "title":{"type":"string","enum":["Pressure and Space","The Turning Tide","A Game of Transitions","Seventy-Two Seconds"]},
          "beats":{"type":"array","minItems":6,"maxItems":9,"items":{"type":"object","additionalProperties":false,
            "required":["id","startTime","endTime","kind","eventIds","playerIds"],"properties":{
              "id":{"type":"string"},"startTime":{"type":"number"},"endTime":{"type":"number"},
              "kind":{"type":"string","enum":["build-up","turnover","counterattack","support-run","finish","response"]},
              "eventIds":{"type":"array","items":{"type":"string"}},"playerIds":{"type":"array","items":{"type":"string"}}}}}
        }}
        }}
        """;
}
#pragma warning restore GHCP001
