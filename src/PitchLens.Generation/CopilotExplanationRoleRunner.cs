using System.Text.Json;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

namespace PitchLens.Generation;

#pragma warning disable GHCP001
public sealed class CopilotExplanationRoleRunner(string cliPath,string workingDirectory,string? witnessDirectory=null):IExplanationRoleRunner
{
    public AgentExecution? Execution{get;private set;}
    public static SessionConfig Config(ExplanationRole role,MomentTools? tools)
    {
        var config=role==ExplanationRole.AudienceEditor?CopilotScenarioSource.CreateSessionConfig():
            CopilotMomentSource.CreateConfig(tools??throw new ArgumentException("Evidence role needs captured read-only tools."));
        config.SystemMessage=new(){Mode=SystemMessageMode.Replace,Content=ExplanationTeamContract.Instructions(role)};
        return config;
    }
    public async Task<string> RunAsync(TeamRoleRequest request,MomentEvidence evidence,TimeSpan remaining,
        IProgress<RequestProgress>? progress,CancellationToken token)
    {
        var name=request.Role.ToString();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(remaining);
        MomentTools? tools=null;
        if(request.Role!=ExplanationRole.AudienceEditor)tools=new MomentTools(evidence,deadline,
            new InlineProgress<RequestProgress>(value=>Update(value.Message)));
        Execution=new(name,CopilotScenarioSource.Model,"Starting",0,0,0,0,request.InputHash,null,[]);
        void Update(string status)
        {
            Execution=Execution! with{Status=status,ToolCalls=tools?.Calls??0,Reads=tools?.Audit??[]};
            progress?.Report(new(RequestStage.Thinking,$"{name}: {status}",tools?.Calls??0));
        }
        async Task Save(string file,string value)
        {
            if(witnessDirectory is null)return;
            if(value.Length>1_000_000)throw new CopilotGenerationException("Team witness exceeds diagnostic size bound.");
            var folder=Path.Combine(witnessDirectory,name);Directory.CreateDirectory(folder);
            var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Options=FileOptions.Asynchronous};
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            await using var stream=new FileStream(Path.Combine(folder,file),options);
            await using var writer=new StreamWriter(stream);await writer.WriteAsync(value);
        }
        try
        {
            await Save("input.json",request.InputJson);
            await Save("instructions.txt",ExplanationTeamContract.Instructions(request.Role));
            await Save("schema.json",ExplanationTeamContract.Schema(request.Role));
            if(tools is not null){await Save("observed-match-schema.json",tools.Index.JsonSchema.GetRawText());await Save("observed-event-schema.json",tools.Event.JsonSchema.GetRawText());}
            var client=new CopilotScenarioSource(cliPath,workingDirectory).CreateClient();
            await using var owner=new CopilotScenarioSource.BoundedClient(client,()=>Update("Cleaning up owned runtime"));
            await CopilotScenarioSource.ConnectAsync(client,null,status=>Update(status),deadline.Token).ConfigureAwait(false);
            var session=await client.CreateSessionAsync(Config(request.Role,tools),deadline.Token).ConfigureAwait(false);
            Execution=Execution! with{SessionsCreated=1};Update("Separate session created");
            try
            {
                using var watch=session.On<SessionEvent>(evt=>{if(evt is AssistantTurnStartEvent)Update("Working on role output");});
                using var schema=JsonDocument.Parse(ExplanationTeamContract.Schema(request.Role));
                Update("Sending request");
                Execution=Execution! with{SdkSends=1};
                var raw=await session.SendAndWaitAsync(new MessageOptions{
                    Prompt=$"Role {name}. Copy identity and inputHash={request.InputHash} exactly. Typed frozen input:\n"+request.InputJson,
                    ResponseSchema=schema.RootElement.Clone()},remaining,deadline.Token).ConfigureAwait(false);
                var content=raw?.Data.Content??throw new CopilotGenerationException($"{name} returned no output.");
                Execution=Execution! with{Responses=1};Update("Response received");
                await Save("response.json",content);
                if(tools is not null&&(!tools.ReadIndex||!tools.ReadFocus||tools.Calls>6))
                    throw new CopilotGenerationException($"{name} must inspect captured index/focus within six reads.");
                return content;
            }
            finally
            {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await session.AbortAsync(cleanup.Token).ConfigureAwait(false);
            }
        }
        catch(Exception error) when(error is IOException or InvalidOperationException or TimeoutException or RemoteRpcException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception or AggregateException)
        {Update("Failed");throw new CopilotGenerationException($"{name}: {CopilotDiagnostics.Describe("response",error).Message}",error);}
        finally
        {
            Update(deadline.IsCancellationRequested?"Cancelled / deadline":Execution!.Responses==1?"Session closed":"Stopped");
            if(witnessDirectory is not null)
                await Save("execution.json",JsonSerializer.Serialize(new TeamReport(request.Identity,ExplanationTeamContract.Pipeline,[Execution!],null,[],false,null),
                    TeamJsonContext.Default.TeamReport));
        }
    }
}
#pragma warning restore GHCP001
