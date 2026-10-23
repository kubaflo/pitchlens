using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;
using Page=PitchLens.Core.Tests.Components.MatchView;
using Controls=PitchLens.Core.Tests.Components.ViewSettings;

namespace PitchLens.Core.Tests
{
#pragma warning disable BL0006 // Dispatch the real Razor event handlers in this headless interaction regression.
    public sealed class ViewSettingsTests
    {
        [Theory]
        [InlineData(SessionMode.Unchosen,false,false)]
        [InlineData(SessionMode.Live,false,false)]
        [InlineData(SessionMode.Live,true,false)]
        [InlineData(SessionMode.Live,true,true)]
        [InlineData(SessionMode.Test,true,false)]
        [InlineData(SessionMode.Test,true,true)]
        public async Task Reading_buttons_apply_persisted_sizes_in_actual_startup_and_active_Razor_without_playback_or_requests(SessionMode mode,bool ready,bool playing)
        {
            var forbidden=new MatchLensTests.Forbidden();string? saved=null;var writes=0;
            using var service=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No auth"),forbidden,
                mode:mode,readyDemo:ready,replayClock:new Clock(),saveReading:value=>{saved=ReadingSettings.Serialize(value);writes++;});
            if(ready){service.Command("seek",86);service.Command("rate",3);if(playing)service.Command("play");}
            var before=service.Sample(true);var view=service.ViewSettings;var preferences=service.InsightPreferences;var answer=service.Explainer.Current;
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(service,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            foreach(var percent in new[]{100,125,150,175,200,100})
            {
                await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("reading-"+percent));
                Assert.Equal(percent,service.Reading.Percent);
                if(saved is not null)
                {
                    var restored=ReadingSettings.Load(saved,out var notice);Assert.Null(notice);Assert.Equal(service.Reading,restored);
                    using var reconstructed=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No auth"),reading:restored);
                    Assert.Equal(percent,reconstructed.Reading.Percent);
                }
                var after=service.Sample(true);
                Assert.Equal(before?.Frame.Snapshot.Time,after?.Frame.Snapshot.Time);Assert.Equal(before?.Frame.Playing,after?.Frame.Playing);Assert.Equal(before?.Frame.Rate,after?.Frame.Rate);
                Assert.Equal(before?.Frame.Revision,after?.Frame.Revision);Assert.Equal(view,service.ViewSettings);Assert.Equal(preferences,service.InsightPreferences);
                Assert.Same(answer,service.Explainer.Current);Assert.False(service.Explainer.Busy);Assert.Equal(0,forbidden.Calls);
            }
            Assert.Equal(5,writes);await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("reading-200"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("reading-reset"));Assert.Equal(100,service.Reading.Percent);
            using var htmlServices=Provider(service,new Capture(true));
            await using var htmlRenderer=new HtmlRenderer(htmlServices,htmlServices.GetRequiredService<ILoggerFactory>());
            var root=await htmlRenderer.Dispatcher.InvokeAsync(()=>htmlRenderer.RenderComponentAsync<Page>());
            var html=await htmlRenderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("data-open-popup=\"reading-dialog\"",html);Assert.Contains("--reading-scale:1;",html);
            Assert.Contains("Text size",html);Assert.Contains("reading-reset",html);
        }
        [Theory]
        [InlineData("{\"version\":1,\"percent\":99}")]
        [InlineData("{\"version\":1,\"percent\":201}")]
        [InlineData("{\"version\":1,\"percent\":110}")]
        [InlineData("{\"version\":2,\"percent\":150}")]
        [InlineData("{\"version\":1,\"percent\":150,\"percent\":200}")]
        [InlineData("{\"percent\":150}")]
        [InlineData("{\"version\":1,\"percent\":150,\"extra\":true}")]
        [InlineData("broken")]
        public async Task Invalid_reading_preferences_have_visible_notice_and_failed_save_keeps_old_value(string json)
        {
            var value=ReadingSettings.Load(json,out var notice);Assert.Equal(ReadingSettings.Default,value);Assert.NotNull(notice);
            var forbidden=new MatchLensTests.Forbidden();
            using var native=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,
                reading:value,readingNotice:notice,saveReading:_=>throw new IOException("Controlled text preference write failure."));
            native.Command("seek",86);native.Command("play");
            var capture=new Capture(true);using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            Assert.Contains("Saved text size is invalid",await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ReadingForTest(new(Percent:150)));
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);Assert.Contains("Controlled text preference write failure",html);
            Assert.Contains("role=\"alert\"",html);Assert.Contains("--reading-scale:1;",html);Assert.Equal(value,native.Reading);
            Assert.True(native.Sample(true)!.Frame.Playing);Assert.Equal(0,forbidden.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.ReadingForTest(new(Percent:101)));
            Assert.Contains("25% steps",await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Equal(value,native.Reading);
        }
        [Fact]
        public async Task Reading_size_invalidates_the_existing_renderer_once_and_leaves_recorded_answer_unchanged()
        {
            var forbidden=new MatchLensTests.Forbidden();
            using var native=new MatchService(forbidden,forbidden,(_,_)=>throw new InvalidOperationException("No SDK"),forbidden,
                mode:SessionMode.Test,replayClock:new Clock());
            native.Command("seek",86);native.LoadRecordedMoment();var answer=native.Explainer.Current;
            Assert.NotNull(answer);
            var capture=new Capture(true);using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var module=new Module();
            await renderer.Dispatcher.InvokeAsync(async()=>{
                capture.Page!.AttachViewModuleForTest(module);await capture.Page.RefreshViewForTest();
                var initial=module.Calls.Count(x=>x=="setReadingSize");
                native.SetReading(new(Percent:200));await capture.Page.RefreshViewForTest();
                Assert.Equal(initial+1,module.Calls.Count(x=>x=="setReadingSize"));
                for(var i=0;i<4;i++)await capture.Page.RefreshViewForTest();
                Assert.Equal(initial+1,module.Calls.Count(x=>x=="setReadingSize"));
            });
            Assert.Same(answer,native.Explainer.Current);Assert.Equal(0,forbidden.Calls);
        }
        [Theory]
        [InlineData(0,"queued")]
        [InlineData(1,"running")]
        [InlineData(2,"running")]
        [InlineData(3,"running")]
        [InlineData(3,"cleanup")]
        [InlineData(3,"approved")]
        [InlineData(2,"rejected")]
        [InlineData(2,"cancelled")]
        [InlineData(3,"timeout")]
        public async Task Actual_agent_cards_explain_order_and_do_not_claim_review_approval_early(int count,string state)
        {
            var source=new ExplanationTeamSource(new ExplanationTeamTests.Roles());
            await source.ExplainAsync(ExplanationTeamTests.Evidence(),CancellationToken.None);
            var approved=source.Collaboration!;
            var stages=approved.Stages.Take(count).ToArray();
            if(count>0&&state!="approved")stages[^1]=stages[^1] with{Status=state=="cleanup"?"Cleaning up owned runtime":"Working on role output",Responses=state=="cleanup"?1:0,OutputHash=null};
            var report=approved with{Stages=stages,Approved=state=="approved",Checks=state=="approved"?approved.Checks:[],
                Failure=state switch{"rejected"=>"Evidence review rejected the candidate.","cancelled"=>"Request cancelled.","timeout"=>"Whole deadline timed out.",_=>null}};
            using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Components.AgentCollaboration>(
                ParameterView.FromDictionary(new Dictionary<string,object?>{{"Report",report}})));
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            var technical=html.IndexOf("<details",StringComparison.Ordinal);var visible=html[..technical];
            Assert.Contains("Find the evidence",visible);Assert.Contains("Write the explanation",visible);Assert.Contains("Check the evidence",visible);
            Assert.Contains("Receives the inspected findings",visible);Assert.Contains("independently reads every claim",visible);
            Assert.DoesNotContain(approved.Identity.RunId,visible);Assert.DoesNotContain(approved.Identity.FactsHash,visible);
            Assert.DoesNotContain("gpt-6.1-sol",visible);Assert.DoesNotContain("<details open",html);Assert.Contains("Technical details",html);
            Assert.Equal(state=="approved",visible.Contains("All 3 steps completed"));
            Assert.Equal(3-count,visible.Split("Not started").Length-1);
            if(state=="cleanup"){Assert.Contains("Validating / finishing",visible);Assert.DoesNotContain("Review accepted",visible);}
            if(state=="cancelled")Assert.Contains("Cancelled",visible);
            if(state=="timeout")Assert.Contains("Timed out",visible);
            if(state=="rejected")Assert.Contains("Rejected / stopped",visible);
        }
        [Fact]
        public async Task Actual_Razor_Explain_starts_one_team_and_renders_actual_collaboration_not_static_agent_labels()
        {
            var roles=new ExplanationTeamTests.Roles();var team=new ExplanationTeamSource(roles);var other=new FixtureSelectionTests.CaptureSource();
            using var native=new MatchService(other,team,(_,_)=>throw new InvalidOperationException("No auth"),other,replayClock:new Clock());
            native.Command("seek",86);
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("explain-button"));
            Assert.Equal(3,roles.Inputs.Count);Assert.True(native.Explainer.Current!.Collaboration!.Approved);
            Assert.Contains("openPopup:moment-dialog",module.Calls);Assert.False(native.Sample(true)!.Frame.Playing);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("explain-button"));
            Assert.Equal(3,roles.Inputs.Count);Assert.True(native.Explainer.Current.Cached);
            var cached=native.Explainer.Current;native.SetReading(new(Percent:150));
            Assert.Same(cached,native.Explainer.Current);Assert.Equal(3,roles.Inputs.Count);
            using var htmlServices=Provider(native,new Capture());
            await using var htmlRenderer=new HtmlRenderer(htmlServices,htmlServices.GetRequiredService<ILoggerFactory>());
            var root=await htmlRenderer.Dispatcher.InvokeAsync(()=>htmlRenderer.RenderComponentAsync<Page>());
            var html=System.Net.WebUtility.HtmlDecode(await htmlRenderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("How your explanation is made",html);Assert.Contains("EvidenceAnalyst",html);Assert.Contains("AudienceEditor",html);Assert.Contains("EvidenceReviewer",html);
            Assert.Contains(native.Explainer.Current.Collaboration.CandidateHash!,html);Assert.Contains("270s whole team",html);
            Assert.Contains("Sends 1",html);Assert.Contains("Tool calls 0",html);
            if(Environment.GetEnvironmentVariable("PITCHLENS_TEAM_UI_WITNESS") is {Length:>0} file)
                await File.WriteAllTextAsync(file,html);
            if(Environment.GetEnvironmentVariable("PITCHLENS_TEAM_REAL_WITNESS") is {Length:>0} folder&&
                Environment.GetEnvironmentVariable("PITCHLENS_TEAM_ACCEPTED_MATCH") is {Length:>0} input)
            {
                var bytes=await File.ReadAllBytesAsync(input);
                Assert.Equal("0332e31623b43f17a6b48c16555076eb0a5c2a52895fc924b6d17aa1ccffc1bf",
                    Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
                var report=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(folder,"team-report.json")),TeamJsonContext.Default.TeamReport)!;
                var analyst=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(folder,"EvidenceAnalyst","response.json")),TeamJsonContext.Default.AnalystPacket)!;
                var editor=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(folder,"AudienceEditor","response.json")),TeamJsonContext.Default.EditorPacket)!;
                var reviewer=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(folder,"EvidenceReviewer","response.json")),TeamJsonContext.Default.ReviewerPacket)!;
                Assert.Equal(report.Identity,analyst.Identity);Assert.Equal(analyst.Identity,editor.Identity);Assert.Equal(editor.Identity,reviewer.Identity);
                Assert.All(analyst.Findings,f=>Assert.InRange(f.Text.Length,8,220));
                Assert.All(editor.Answer.Fan.Concat(editor.Answer.Analyst),c=>Assert.InRange(c.Text.Length,8,280));
                Assert.Equal(ExplanationTeamContract.Hash(System.Text.Json.JsonSerializer.Serialize(analyst,TeamJsonContext.Default.AnalystPacket)),report.Stages[0].OutputHash);
                Assert.Equal(ExplanationTeamContract.Hash(System.Text.Json.JsonSerializer.Serialize(editor,TeamJsonContext.Default.EditorPacket)),report.Stages[1].OutputHash);
                Assert.Equal(ExplanationTeamContract.Hash(System.Text.Json.JsonSerializer.Serialize(reviewer,TeamJsonContext.Default.ReviewerPacket)),report.Stages[2].OutputHash);
                Assert.Equal(ExplanationTeamContract.Claims(editor.Answer).Select(c=>c.Id),reviewer.Claims.Select(c=>c.ClaimId));
                var reviewerIds=report.Stages[2].Reads.Where(r=>r.Tool=="observed_event").Select(r=>r.EventId).ToHashSet();
                Assert.All(editor.Answer.Fan.Concat(editor.Answer.Analyst).SelectMany(c=>c.EvidenceIds),id=>Assert.Contains(id,reviewerIds));
                var match=AuthoredMatchPlan.Parse(System.Text.Encoding.UTF8.GetString(bytes),false);
                var evidence=MomentEvidence.Capture(match,report.Identity.Revision,report.Identity.Cutoff,report.Identity.FocusId,
                    preferences:new(report.Identity.Task,report.Identity.Scope,report.Identity.SubjectId));
                var answer=evidence.Validate(await File.ReadAllTextAsync(Path.Combine(folder,"validated-answer.json")));
                Assert.Equal(report.CandidateHash,ExplanationTeamContract.Hash(ExplanationTeamContract.AnswerJson(answer)));
                var result=new ExplainedMoment(evidence,answer,report.Stages.Sum(s=>s.ToolCalls),PipelineVersion:report.Pipeline,Collaboration:report);
                var page=await htmlRenderer.Dispatcher.InvokeAsync(()=>htmlRenderer.RenderComponentAsync<Components.MomentStudio>(
                    ParameterView.FromDictionary(new Dictionary<string,object?>{{"Result",result}})));
                var actual=System.Net.WebUtility.HtmlDecode(await htmlRenderer.Dispatcher.InvokeAsync(page.ToHtmlString));
                Assert.Contains(report.Identity.RunId,actual);Assert.Contains(report.CandidateHash!,actual);
                Assert.Contains("EvidenceReviewer",actual);Assert.Contains("Sends 1",actual);
                if(Environment.GetEnvironmentVariable("PITCHLENS_READING_PREVIEW") is {Length:>0} preview)
                {
                    Directory.CreateDirectory(preview);
                    foreach(var percent in new[]{100,150,200})
                    {
                        foreach(var pending in new[]{false,true})
                        {
                            var value=pending?report with{Approved=false,Checks=[],Stages=[report.Stages[0] with{Status="Cleaning up owned runtime",OutputHash=null}]}:report;
                            var component=await htmlRenderer.Dispatcher.InvokeAsync(()=>htmlRenderer.RenderComponentAsync<Components.AgentCollaboration>(
                                ParameterView.FromDictionary(new Dictionary<string,object?>{{"Report",value}})));
                            var markup=pending?await htmlRenderer.Dispatcher.InvokeAsync(component.ToHtmlString):actual;
                            var name=$"{(pending?"controlled-pending":"actual-approved")}-{percent}.html";
                            var title=pending?"Controlled pending state using the existing report":"Existing actual approved team and unchanged answer";
                            var document="<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>PitchLens reading preview</title><link rel=\"stylesheet\" href=\"app.css\"><style>"+new ReadingSettings(Percent:percent).Css+"</style></head><body><main style=\"max-width:1120px;margin:auto;padding:24px\"><p class=\"caption\">Static production Razor preview · "+title+" · "+percent+"% · Native handlers disconnected · No new SDK calls · Not native pixel certification</p>"+markup+"</main></body></html>";
                            await File.WriteAllTextAsync(Path.Combine(preview,name),document);
                        }
                    }
                }
                else await File.WriteAllTextAsync(Path.Combine(folder,"actual-production-razor.html"),actual);
            }
        }
        [Theory]
        [InlineData("manual")]
        [InlineData("random")]
        [InlineData("unchosen")]
        [InlineData("blocked")]
        public async Task Actual_Razor_centered_setup_has_one_heading_one_demo_action_and_honest_errors(string state)
        {
            var source=new FixtureSelectionTests.CaptureSource();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,readyDemo:false,
                mode:state=="unchosen"?SessionMode.Unchosen:SessionMode.Live,
                variety:new MatchVariety(saveSelection:_=>{if(state=="blocked")throw new IOException("Selected fixture could not be saved (controlled test).");}));
            await native.StartSessionAsync();
            if(state=="manual")native.SetFixtureChoice(new(FixtureChoiceKind.Manual,"liverpool","chelsea"));
            if(state=="random")native.SetFixtureChoice(FixtureChoice.Random);
            if(state=="blocked"){await native.SubmitFixtureAsync();native.OpenFixturePicker();}
            using var services=Provider(native,new Capture());
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Equal(1,html.Split("<h1",StringSplitOptions.None).Length-1);
            Assert.DoesNotContain(">fixtureError<",html);Assert.DoesNotContain("Ready for a different match",html);
            Assert.DoesNotContain("Your next matchday",html);Assert.Contains("class=\"setup-card\"",html);
            if(state!="unchosen"){Assert.Equal(1,html.Split("Try recorded demo",StringSplitOptions.None).Length-1);Assert.Contains("Choose your teams",html);}
            if(state=="blocked"){Assert.Contains("Selected fixture could not be saved",html);Assert.Contains("role=\"alert\"",html);}
            else Assert.DoesNotContain("class=\"error\" role=\"alert\"",html);
            Assert.Empty(source.Requests);
            if(Environment.GetEnvironmentVariable("PITCHLENS_SETUP_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);
                var preview="<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Actual Razor setup preview — static, no SDK</title><link rel=\"stylesheet\" href=\"app.css\"></head><body><div id=\"app\">"+html+"</div></body></html>";
                await File.WriteAllTextAsync(Path.Combine(folder,$"setup-{state}.html"),preview);
            }
        }
        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public async Task Actual_Razor_picker_never_renders_empty_error_alerts(string error)
        {
            using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Components.FixturePicker>(
                ParameterView.FromDictionary(new Dictionary<string,object?>{{"Error",error}})));
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.DoesNotContain("role=\"alert\"",html);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Actual_Razor_valid_picker_has_no_literal_or_empty_error_and_real_validation_stays_visible(bool existingMatch)
        {
            var source=new FixtureSelectionTests.CaptureSource();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,
                readyDemo:existingMatch,mode:SessionMode.Live);
            if(existingMatch)native.OpenFixturePicker();else await native.StartSessionAsync();
            var capture=new Capture();
            using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var valid=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.DoesNotContain(">fixtureError<",valid);
            Assert.DoesNotContain("class=\"error\" role=\"alert\"",valid);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.FixtureChoiceForCleanupTest(new(FixtureChoiceKind.Manual,"chelsea","chelsea")));
            var invalid=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Home and away clubs must be different",invalid);Assert.Contains("role=\"alert\"",invalid);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.FixtureChoiceForCleanupTest(new(FixtureChoiceKind.Manual,"unknown","chelsea")));
            invalid=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);Assert.Contains("Choose two supported clubs",invalid);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.FixtureChoiceForCleanupTest(FixtureChoice.Random));
            var random=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.DoesNotContain(">fixtureError<",random);Assert.DoesNotContain("class=\"error\" role=\"alert\"",random);
            Assert.Empty(source.Requests);
        }
        [Fact]
        public async Task Actual_Razor_cancel_random_failure_retry_and_Newmatch_all_use_the_same_nonspending_picker()
        {
            var source=new FixtureSelectionTests.CaptureSource{Fail=true};var attempts=0;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,
                readyDemo:false,mode:SessionMode.Live,variety:new MatchVariety(saveSelection:_=>attempts++));
            var capture=new Capture();var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-random"));
            Assert.Equal(FixtureChoice.Random,native.FixtureChoice);Assert.Empty(source.Requests);Assert.Equal(0,attempts);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-cancel"));
            Assert.False(native.FixturePickerOpen);Assert.Empty(source.Requests);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("choose-startup-teams"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-create"));
            Assert.Equal(GenerationState.Failed,native.RequestState);Assert.Single(source.Requests);Assert.Equal(1,attempts);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("choose-startup-teams"));
            Assert.Single(source.Requests);Assert.Equal(1,attempts);Assert.True(native.FixturePickerOpen);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-manual"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("fixture-home","liverpool"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("fixture-away","chelsea"));
            source.Fail=false;
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-create"));
            Assert.True(native.HasMatch);Assert.Equal(2,source.Requests.Count);Assert.Equal(2,attempts);
            native.Command("play");
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("generate-match"));
            Assert.True(native.FixturePickerOpen);Assert.True(native.Sample(true)!.Frame.Playing);
            Assert.Equal(2,source.Requests.Count);Assert.Contains("openPopup:fixture-dialog",module.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-cancel"));
            Assert.True(native.Sample(true)!.Frame.Playing);Assert.Equal(2,source.Requests.Count);
        }
        [Theory]
        [InlineData("liverpool","chelsea",SessionMode.Unchosen)]
        [InlineData("manchester-united","tottenham-hotspur",SessionMode.Live)]
        public async Task Actual_Razor_startup_fixture_choices_wait_for_Create_and_send_exact_22_catalog_players(string home,string away,SessionMode initialMode)
        {
            var source=new FixtureSelectionTests.CaptureSource();var historyWrites=0;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No auth before Create"),source,
                readyDemo:false,mode:initialMode,variety:new MatchVariety(saveSelection:_=>historyWrites++));
            var capture=new Capture();var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            if(initialMode==SessionMode.Unchosen)await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("first-live-choice"));
            Assert.True(native.FixturePickerOpen);Assert.Empty(source.Requests);Assert.Equal(0,historyWrites);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("fixture-home",home));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("fixture-away",away));
            Assert.Empty(source.Requests);Assert.Equal(0,historyWrites);Assert.False(native.GenerationActivity.Snapshot.Active);
            if(Environment.GetEnvironmentVariable("PITCHLENS_FIXTURE_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);
                using var htmlServices=Provider(native,new Capture());
                await using var htmlRenderer=new HtmlRenderer(htmlServices,htmlServices.GetRequiredService<ILoggerFactory>());
                var html=await htmlRenderer.Dispatcher.InvokeAsync(()=>htmlRenderer.RenderComponentAsync<Page>());
                var markup=System.Net.WebUtility.HtmlDecode(await htmlRenderer.Dispatcher.InvokeAsync(html.ToHtmlString));
                Assert.Contains(FixtureCatalog.Clubs.Single(t=>t.Id==home).Name,markup);
                Assert.Contains(FixtureCatalog.Clubs.Single(t=>t.Id==away).Name,markup);
                await File.WriteAllTextAsync(Path.Combine(folder,$"{home}-{away}-before-create.html"),markup);
            }
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("fixture-create"));
            var requested=Assert.Single(source.Requests).Fixture!;
            Assert.Equal(home,requested.Home.Id);Assert.Equal(away,requested.Away.Id);
            Assert.Equal(FixtureCatalog.Create(home,away).Players,requested.Players);
            Assert.Equal(GenerationState.Succeeded,native.RequestState);Assert.Equal(1,historyWrites);
            Assert.Equal(new[]{home,away},native.Match.Teams.Select(t=>t.Id));
            await native.StartSessionAsync();Assert.Single(source.Requests);
            if(Environment.GetEnvironmentVariable("PITCHLENS_FIXTURE_WITNESS") is {Length:>0} output)
                await File.WriteAllTextAsync(Path.Combine(output,$"{home}-{away}-handler-path.json"),
                    System.Text.Json.JsonSerializer.Serialize(new{evidence="Actual production Razor click/change dispatch with controlled generation source, not paid/native UI",
                        initialMode,requestedFixture=requested.Id,roster=requested.Players.Select(p=>p.Id),sourceCalls=source.Requests.Count,historyWrites}));
        }
        [Fact]
        public async Task Actual_Razor_task_focus_and_tailor_handlers_condition_source_without_double_spend()
        {
            var source=new PersonalizedInsightTests.Source();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,replayClock:new Clock());
            native.Command("seek",86);
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("explain-button"));
            Assert.Equal(1,source.Calls);var first=native.Explainer.Current!;
            Assert.False(native.Sample(true)!.Frame.Playing);Assert.Contains("openPopup:moment-dialog",module.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("insight-task","1"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ChangeAsync("insight-focus","club:manchester-city"));
            Assert.Equal(1,source.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("tailor-explain-button"));
            Assert.Equal(2,source.Calls);Assert.NotEqual(first.Evidence.Hash,native.Explainer.Current!.Evidence.Hash);
            Assert.Equal(InsightTask.ExplainBuildUp,native.Explainer.Current.Evidence.Preferences.Task);
            Assert.Equal("manchester-city",native.Explainer.Current.Evidence.Preferences.SubjectId);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("tailor-explain-button"));
            Assert.Equal(2,source.Calls);Assert.True(native.Explainer.Current.Cached);
            Assert.Equal(86,native.Sample(true)!.Frame.Snapshot.Time);
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Actual_Razor_tour_and_ledger_content_is_honest_about_recorded_reads(bool fresh)
        {
            var source=new PersonalizedInsightTests.Source();
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException(),source,
                mode:fresh?SessionMode.Live:SessionMode.Test,replayClock:new Clock());
            if(fresh){native.Command("seek",86);await native.ExplainCurrentMomentAsync();}
            else{native.StartGuidedDemo();native.SetGuidedDemoStep(1);}
            using var services=Provider(native,new Capture());
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            Assert.Contains("How this was checked",html);
            Assert.Contains("not prove every interpretation true",html);
            if(fresh){Assert.Contains("observed_match",html);Assert.Contains("observed_event",html);Assert.Equal(2,native.Explainer.Current!.Ledger!.Length);}
            else{Assert.Contains("Individual read metadata was not recorded",html);Assert.Contains("Guided demo · No AI calls",html);Assert.Equal(0,source.Calls);}
            if(Environment.GetEnvironmentVariable("PITCHLENS_P0_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,fresh?"controlled-fresh-ledger.html":"recorded-tour.html"),html);
                if(fresh&&Environment.GetEnvironmentVariable("PITCHLENS_P0_REAL_WITNESS") is {Length:>0} real&&
                    Environment.GetEnvironmentVariable("PITCHLENS_P0_ACCEPTED_MATCH") is {Length:>0} input)
                {
                    var bytes=await File.ReadAllBytesAsync(input);
                    Assert.Equal("0332e31623b43f17a6b48c16555076eb0a5c2a52895fc924b6d17aa1ccffc1bf",
                        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
                    var match=AuthoredMatchPlan.Parse(System.Text.Encoding.UTF8.GetString(bytes),false);
                    foreach(var task in new[]{"ExplainBuildUp","PlayerContribution"})
                    {
                        var path=Path.Combine(real,task);
                        var facts=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"moment-facts.json")),MomentJsonContext.Default.MomentFacts)!;
                        var evidence=MomentEvidence.Capture(match,facts.Revision,facts.Cutoff,facts.FocusId,preferences:facts.Context!.Preferences);
                        var answer=evidence.Validate(await File.ReadAllTextAsync(Path.Combine(path,"validated.json")));
                        var ledger=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"ledger.json")),MomentJsonContext.Default.MomentReadAuditArray)!;
                        var counts=System.Text.Json.JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(path,"moment-counts.json")),MomentWitnessJsonContext.Default.MomentRequestCounts)!;
                        Assert.Equal(1,counts.SdkSendInvocations);Assert.Equal(1,counts.Responses);Assert.InRange(counts.ToolCalls,ledger.Length,6);
                        Assert.InRange(ledger.Length,2,6);
                        var actual=new ExplainedMoment(evidence,answer,counts.ToolCalls,Ledger:ledger);
                        var rendered=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Components.MomentStudio>(
                            ParameterView.FromDictionary(new Dictionary<string,object?>{{"Result",actual}})));
                        var actualHtml=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString));
                        Assert.Contains(evidence.Hash,actualHtml);Assert.Contains("observed_match",actualHtml);
                        Assert.Contains(facts.Context.SubjectName!,actualHtml);Assert.Contains(answer.Fan[0].Text,actualHtml);
                        await File.WriteAllTextAsync(Path.Combine(folder,$"actual-backend-{task}.html"),actualHtml);
                    }
                }
            }
        }
        [Fact]
        public async Task Actual_Razor_guided_choice_steps_back_and_exit_never_call_SDK_or_start_Live()
        {
            var source=new MatchLensTests.Forbidden();SessionMode? saved=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                readyDemo:false,mode:SessionMode.Unchosen,saveMode:m=>saved=m,replayClock:new Clock(),filmClock:new Clock());
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("first-guided-demo"));
            Assert.Equal(SessionMode.Test,saved);Assert.Equal(0,native.GuidedDemoStep);
            Assert.Equal(76,native.Sample(true)!.Frame.Snapshot.Time);Assert.Equal(0,source.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-next"));
            Assert.Equal(1,native.GuidedDemoStep);Assert.Equal(86,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.Contains("openPopup:moment-dialog",module.Calls);Assert.False(native.Explainer.Current!.Fresh);
            Assert.Equal(InsightPreferences.Default,native.Explainer.Current.Evidence.Preferences);
            Assert.Null(native.Explainer.Current.Ledger);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("moment-tour-next"));
            Assert.Equal(2,native.GuidedDemoStep);Assert.Contains("setEvidence",module.Calls);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-next"));
            Assert.True(native.FilmActive);Assert.Equal(58,native.FilmFrame!.Duration);Assert.False(native.Director.Current!.Fresh);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-back"));
            Assert.False(native.FilmActive);Assert.Equal(2,native.GuidedDemoStep);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("main-tour-exit"));
            Assert.Null(native.GuidedDemoStep);Assert.False(native.Sample(true)!.Frame.Playing);Assert.Equal(0,source.Calls);
            Assert.Equal(SessionMode.Test,native.Mode);
            if(Environment.GetEnvironmentVariable("PITCHLENS_P0_WITNESS") is {Length:>0} folder)
            {Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"tour-handler-path.json"),
                System.Text.Json.JsonSerializer.Serialize(new{mode=native.Mode,sdkCalls=source.Calls,moduleCalls=module.Calls,renderer.PendingPopups,ExitPaused=!native.Sample(true)!.Frame.Playing}));}
        }
        [Theory]
        [InlineData(SessionMode.Test)]
        [InlineData(SessionMode.Live)]
        public async Task Replay_settings_camera_entry_dispatches_actual_Razor_clicks_without_resuming_the_paused_match(SessionMode mode)
        {
            var source=new MatchLensTests.Forbidden();string? stored=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                mode:mode,replayClock:new Clock(),saveViewSettings:value=>stored=PitchViewSettings.Serialize(value));
            native.Command("seek",100);native.Command("highlights",0);native.Command("rate",3);native.Command("play");
            var capture=new Capture(true);var module=new Module();
            using var services=Provider(native,capture,new ModuleRuntime(module));
            await using var renderer=new CameraEventRenderer(services,capture);
            await renderer.Dispatcher.InvokeAsync(renderer.StartAsync);
            var selected=native.Sample(true)!.Frame.SelectedId;
            var phases=new List<object>();
            void Record(string phase)
            {
                var f=native.Sample(true)!.Frame;
                phases.Add(new{phase,time=f.Snapshot.Time,f.Playing,f.Rate,f.SelectedId,settings=native.ViewSettings,sdkCalls=source.Calls});
                Assert.Equal(100,f.Snapshot.Time);Assert.Equal(3,f.Rate);Assert.Equal(selected,f.SelectedId);Assert.Equal(0,source.Calls);
            }
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("more-button"));
            Assert.True(native.Sample(true)!.Frame.Playing);
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("settings-button"));
            Assert.Contains("openPopup:settings-dialog",module.Calls);Assert.False(native.Sample(true)!.Frame.Playing);
            Record("Replay settings opened");
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("settings-camera-button"));
            Assert.Contains("view-dialog",renderer.PendingPopups);
            Assert.Equal("openPopup:view-dialog",module.Calls.Last(c=>c.StartsWith("openPopup:")));
            Assert.False(native.Sample(true)!.Frame.Playing);Record("Settings camera entry opened shared popup");
            foreach(var style in Enum.GetValues<PlayerMarkerStyle>())
            {
                await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(style));
                Assert.True(await renderer.Dispatcher.InvokeAsync(()=>renderer.HasButton("view-wide-camera")));
                Assert.True(await renderer.Dispatcher.InvokeAsync(()=>renderer.HasButton("view-detail-camera")));
                Record($"Camera framing available for {style}");
                await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-detail-camera"));
                Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);Assert.True(native.ViewSettings.SmartDetail);
                Record($"Detail explicitly selected Smart zoom from {style}");
            }
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-detail-camera"));
            Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);Assert.True(native.ViewSettings.SmartDetail);
            Assert.False(native.Sample(true)!.Frame.Playing);Record("Detail explicitly selects Smart zoom");
            Assert.Equal(native.ViewSettings,PitchViewSettings.Load(stored,out _));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-wide-camera"));
            Assert.False(native.ViewSettings.SmartDetail);Assert.Equal(PlayerMarkerStyle.SmartZoom,native.ViewSettings.Style);
            Record("Wide restores Smart zoom wide camera");
            native.Command("play");
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("more-button"));
            await renderer.Dispatcher.InvokeAsync(()=>renderer.ClickAsync("view-button"));
            Assert.True(native.Sample(true)!.Frame.Playing);Record("Direct More camera entry does not pause");
            if(Environment.GetEnvironmentVariable("PITCHLENS_CAMERA_PATH_WITNESS") is {Length:>0} folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder,$"camera-path-{mode}.json"),
                    System.Text.Json.JsonSerializer.Serialize(new{evidence="Actual Razor render-tree click dispatch; no native/browser UI",phases,pendingPopups=renderer.PendingPopups,calls=module.Calls}));
            }
        }
        [Theory]
        [InlineData(SessionMode.Test,false)]
        [InlineData(SessionMode.Test,true)]
        [InlineData(SessionMode.Live,false)]
        [InlineData(SessionMode.Live,true)]
        public async Task Actual_Razor_six_styles_size_badges_and_camera_apply_without_pause_seek_rate_or_SDK(SessionMode mode,bool playing)
        {
            var source=new MatchLensTests.Forbidden();var clock=new Clock();string? stored=null;
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,mode:mode,
                replayClock:clock,saveViewSettings:value=>stored=PitchViewSettings.Serialize(value));
            native.Command("seek",100);native.Command("highlights",0);native.Command("rate",3);
            if(playing)native.Command("play");
            var capture=new Capture();using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var module=new Module();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.AttachViewModuleForTest(module));
            var original=native.Sample(true)!.Frame;
            var hash=NarrationPrompt.Hash(native.Match);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.OpenViewForTest());
            Assert.Contains("openPopup:view-dialog",module.Calls);
            foreach(var style in Enum.GetValues<PlayerMarkerStyle>())
            {
                await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(style));
                await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshViewForTest());
                Assert.Equal(style,native.ViewSettings.Style);
                Assert.NotNull(module.LastSettings);
                Assert.Equal(native.ViewSettings,PitchViewSettings.Load(module.LastSettings,out _));
                var frame=native.Sample(true)!.Frame;
                Assert.Equal(original.Snapshot.Time,frame.Snapshot.Time);Assert.Equal(playing,frame.Playing);
                Assert.Equal(original.SelectedId,frame.SelectedId);Assert.Equal(3,frame.Rate);
                Assert.Equal(hash,NarrationPrompt.Hash(native.Match));
                var styleHtml=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
                Assert.DoesNotContain("class=\"segments camera-controls\" hidden",styleHtml);
                Assert.Contains("Wide or Detail selects the Smart zoom style",styleHtml);
            }
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetSizeForTest("1.25"));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetBadgesForTest(false));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetDetailForTest(true));
            Assert.Equal(1.25,native.ViewSettings.MarkerScale);Assert.False(native.ViewSettings.ShowNumbers);Assert.True(native.ViewSettings.SmartDetail);
            var interop=module.SettingsWrites;
            for(var i=0;i<4;i++)await renderer.Dispatcher.InvokeAsync(()=>capture.Page!.RefreshViewForTest());
            Assert.Equal(interop,module.SettingsWrites); // Repeated paused/native samples do not resend visual settings.
            Assert.Equal(0,source.Calls);Assert.NotNull(stored);
            var restored=PitchViewSettings.Load(stored,out var notice);Assert.Null(notice);
            using var relaunched=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                mode:mode,viewSettings:restored);
            Assert.Equal(native.ViewSettings,relaunched.ViewSettings);
            var html=System.Net.WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            foreach(var label in new[]{"Camera & player appearance","Camera framing","Wide · Smart zoom","Detail · Smart zoom","Numbers","Photo tokens","Cut-out players","Portrait tiles","Focus first","Smart zoom","Player marker size","Shirt-number badges"})
                Assert.Contains(label,html);
            Assert.Contains("<dialog id=\"view-dialog\"",html);
            Assert.DoesNotContain("id=\"view-button\" aria-haspopup",html);
            Assert.Contains("id=\"settings-camera-button\"",html);
            Assert.True(html.IndexOf("class=\"camera-entry\"",StringComparison.Ordinal)<html.IndexOf("id=\"scenario-provenance\"",StringComparison.Ordinal));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.ResetForTest());
            Assert.Equal(PitchViewSettings.Default,native.ViewSettings);
            Assert.Equal(PitchViewSettings.Default,PitchViewSettings.Load(stored,out _));
            Assert.Equal(playing,native.Sample(true)!.Frame.Playing);Assert.Equal(100,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.Equal(0,source.Calls);
            if(Environment.GetEnvironmentVariable("PITCHLENS_VIEW_WITNESS") is {Length:>0} folder)
            {Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,$"view-{mode}-{playing}.html"),html);}
        }
        [Theory]
        [InlineData("{\"version\":1}")]
        [InlineData("{\"version\":2,\"style\":1,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":99,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":1,\"markerScale\":99,\"showNumbers\":true,\"smartDetail\":false}")]
        [InlineData("{\"version\":1,\"style\":1,\"markerScale\":1,\"showNumbers\":true,\"smartDetail\":false,\"url\":\"https://invalid\"}")]
        [InlineData("not-json")]
        public void Invalid_persisted_values_have_visible_default_notice_and_never_bind_an_unvalidated_style(string json)
        {
            var result=PitchViewSettings.Load(json,out var notice);
            Assert.Equal(PitchViewSettings.Default,result);Assert.NotNull(notice);Assert.Contains("invalid",notice);
            Assert.Equal(PitchViewSettings.Default,PitchViewSettings.Load("",out var fresh));Assert.Null(fresh);
            Assert.Throws<ArgumentException>(()=>(result with{MarkerScale=double.NaN}).Validate());
        }
        [Fact]
        public async Task Invalid_input_and_failed_preference_write_show_errors_without_stopping_football_or_mutating_settings()
        {
            var source=new MatchLensTests.Forbidden();var initial=PitchViewSettings.Load("broken",out var notice);
            using var native=new MatchService(source,source,(_,_)=>throw new InvalidOperationException("No SDK"),source,
                viewSettings:initial,viewSettingsNotice:notice,saveViewSettings:_=>throw new IOException("Controlled preference write failure."));
            native.Command("seek",100);native.Command("play");
            var capture=new Capture();using var services=Provider(native,capture);
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Page>());
            var html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Saved view settings are invalid",html);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SelectStyleForTest(PlayerMarkerStyle.PhotoTokens));
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
            Assert.Contains("Controlled preference write failure",html);
            Assert.Equal(initial,native.ViewSettings);Assert.True(native.Sample(true)!.Frame.Playing);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Controls!.SetSizeForTest("invalid"));
            html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);Assert.Contains("Invalid marker size",html);
            Assert.Equal(initial,native.ViewSettings);Assert.Equal(0,source.Calls);
        }
        [Theory]
        [InlineData("arsenal","manchester-city",22)]
        [InlineData("liverpool","chelsea",21)]
        [InlineData("manchester-united","tottenham-hotspur",22)]
        public void Web_portraits_are_catalog_verified_presentation_only_and_missing_photo_has_no_binding(string home,string away,int available)
        {
            var match=AuthoredMatchPlan.Parse(FixtureTests.Json(FixtureCatalog.Create(home,away)));
            using var json=System.Text.Json.JsonDocument.Parse(WebContracts.Scenario(match,3));
            var players=json.RootElement.GetProperty("players").EnumerateArray().ToArray();
            Assert.Equal(available,players.Count(p=>p.GetProperty("portrait").ValueKind==System.Text.Json.JsonValueKind.Object));
            if(away=="chelsea")Assert.Equal(System.Text.Json.JsonValueKind.Null,players.Single(p=>p.GetProperty("id").GetString()=="chelsea-martinez").GetProperty("portrait").ValueKind);
            foreach(var p in players.Where(p=>p.GetProperty("portrait").ValueKind==System.Text.Json.JsonValueKind.Object))
            {
                var portrait=p.GetProperty("portrait");var id=p.GetProperty("id").GetString();
                Assert.Equal(id,portrait.GetProperty("playerId").GetString());
                Assert.Equal($"images/players/{id}.png",portrait.GetProperty("imagePath").GetString());
            }
        }
        private static ServiceProvider Provider(MatchService native,Capture capture,IJSRuntime? runtime=null)=>new ServiceCollection().AddLogging()
            .AddSingleton(native).AddSingleton<IJSRuntime>(runtime??new NoJs()).AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
        private sealed class Clock:IMonotonicClock{public double Seconds=>0;}
        private sealed class NoJs:IJSRuntime
        {public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>throw new InvalidOperationException("No UI.");public ValueTask<T> InvokeAsync<T>(string id,CancellationToken t,object?[]? args)=>InvokeAsync<T>(id,args);}
        private sealed class Module:IJSObjectReference
        {
            public List<string> Calls=[];public int SettingsWrites;public string? LastSettings;
            public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)
            {
                Calls.Add(id=="openPopup"?"openPopup:"+args![0]:id);
                if(id=="setViewSettings"){SettingsWrites++;LastSettings=(string)args![0]!;}
                return ValueTask.FromResult(default(T)!);
            }
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class Capture(bool skipStartup=false):IComponentActivator
        {
            public Page? Page;public Controls? Controls;
            public IComponent CreateInstance(Type type){var c=(IComponent)Activator.CreateInstance(type)!;if(c is Page p){Page=p;if(skipStartup)p.SkipStartupForCameraPathTest();}if(c is Controls v)Controls=v;return c;}
        }
        private sealed class ModuleRuntime(Module module):IJSRuntime
        {
            public ValueTask<T> InvokeAsync<T>(string id,object?[]? args)=>ValueTask.FromResult((T)(object)module);
            public ValueTask<T> InvokeAsync<T>(string id,CancellationToken token,object?[]? args)=>InvokeAsync<T>(id,args);
        }
        private sealed class CameraEventRenderer(IServiceProvider services,Capture capture):Renderer(services,services.GetRequiredService<ILoggerFactory>())
        {
            public override Dispatcher Dispatcher{get;}=Dispatcher.CreateDefault();
            public List<string> PendingPopups=[];
            private int root;
            public Task StartAsync(){root=AssignRootComponentId(InstantiateComponent(typeof(Page)));return RenderRootComponentAsync(root);}
            protected override void HandleException(Exception error)=>throw new InvalidOperationException("Razor interaction failed.",error);
            protected override Task UpdateDisplayAsync(in RenderBatch batch)
            {if(capture.Page?.PendingPopupForCameraTest() is { } id)PendingPopups.Add(id);return Task.CompletedTask;}
            private ulong FindButton(int component,string id,string eventName="onclick")
            {
                var frames=GetCurrentRenderTreeFrames(component);
                for(var i=0;i<frames.Count;i++)
                {
                    var frame=frames.Array[i];
                    if(frame.FrameType==RenderTreeFrameType.Component)
                    {var child=FindButton(frame.ComponentId,id,eventName);if(child!=0)return child;}
                    if(frame.FrameType!=RenderTreeFrameType.Element)continue;
                    var matches=false;ulong handler=0;
                    for(var j=i+1;j<frames.Count&&frames.Array[j].FrameType==RenderTreeFrameType.Attribute;j++)
                    {
                        var a=frames.Array[j];if(a.AttributeName=="id"&&Equals(a.AttributeValue,id))matches=true;
                        if(a.AttributeName==eventName)handler=a.AttributeEventHandlerId;
                    }
                    if(matches)return handler;
                }
                return 0;
            }
            public bool HasButton(string id)=>FindButton(root,id)!=0;
            public Task ClickAsync(string id)
            {var handler=FindButton(root,id);Assert.NotEqual(0UL,handler);return DispatchEventAsync(handler,null,new MouseEventArgs(),true);}
            public Task ChangeAsync(string id,object value)
            {var handler=FindButton(root,id,"onchange");Assert.NotEqual(0UL,handler);return DispatchEventAsync(handler,null,new ChangeEventArgs{Value=value},true);}
        }
    }
#pragma warning restore BL0006
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MatchView
    {
        public void ReadingForTest(ReadingSettings settings)=>ApplyReading(settings);
        public void FixtureChoiceForCleanupTest(FixtureChoice choice){SetFixtureChoice(choice);StateHasChanged();}
        public void AttachViewModuleForTest(IJSObjectReference value){module=value;rendererReady=true;loadedRevision=frame!.Revision;startupRequested=true;}
        public async Task OpenViewForTest(){Open("view-dialog");await OnAfterRenderAsync(false);}
        public Task RefreshViewForTest()=>Refresh(true);
        public void SkipStartupForCameraPathTest()=>startupRequested=true;
        public string? PendingPopupForCameraTest()=>pendingPopup?.Id;
    }
    public partial class ViewSettings
    {
        public Task SelectStyleForTest(PlayerMarkerStyle style)=>Apply(Value with{Style=style});
        public Task SetSizeForTest(string value)=>SizeChanged(new(){Value=value});
        public Task SetBadgesForTest(bool value)=>NumbersChanged(new(){Value=value});
        public Task SetDetailForTest(bool value)=>Apply(Value with{SmartDetail=value});
        public Task ResetForTest()=>Apply(PitchViewSettings.Default);
    }
}
