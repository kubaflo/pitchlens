using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PitchLens.Core;
using PitchLens.Generation;
using Studio=PitchLens.Core.Tests.Components.MomentStudio;
using Story=PitchLens.Core.Tests.Components.NarrativeStory;

namespace PitchLens.Core.Tests
{
    public sealed class FullVoiceRenderingTests
    {
        [Fact]
        public async Task Actual_Razor_lead_and_expanded_claim_Listen_dispatch_complete_Fan_Analyst_text_and_mode_change_stops()
        {
            var speech=new FullVoiceTests.ControlledSpeech();var clock=new FullVoiceTests.Clock();
            using var native=FullVoiceTests.Native(speech,clock);native.Command("seek",86);
            var result=MomentRecording.Load(native.Match,1,86);
            var capture=new Capture();
            using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var callbacks=new Callback();
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Studio>(
                ParameterView.FromDictionary(new Dictionary<string,object?>
                {
                    ["Result"]=result,["Listen"]=EventCallback.Factory.Create<(string Text,long Revision,double Cutoff)>(callbacks,
                        c=>native.ListenTo(c.Text,c.Revision,c.Cutoff)),
                    ["Stop"]=EventCallback.Factory.Create(callbacks,native.StopListening)
                })));
            Assert.Contains("Listen",await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Studio!.ListenLeadForTest());await speech.WaitCount(1);
            Assert.Equal(result.Answer.Fan[0].Text,speech.Text[0]);
            speech.Complete();await speech.WaitIdle();
            await renderer.Dispatcher.InvokeAsync(()=>capture.Studio!.AudienceForVoiceTest(true));
            await renderer.Dispatcher.InvokeAsync(()=>capture.Studio!.ListenClaimForTest(2));await speech.WaitCount(2);
            Assert.Equal(result.Answer.Analyst[2].Text,speech.Text[1]);
            await renderer.Dispatcher.InvokeAsync(()=>capture.Studio!.AudienceForVoiceTest(false));await speech.WaitIdle();
            Assert.False(native.Voice.Manual);Assert.False(native.Sample(true)!.Frame.Playing);
        }
        [Theory]
        [InlineData(ExplanationMode.Casual)]
        [InlineData(ExplanationMode.Analyst)]
        public async Task Every_visible_story_chapter_Listen_dispatches_entire_current_audience_comment(ExplanationMode mode)
        {
            var speech=new FullVoiceTests.ControlledSpeech();var clock=new FullVoiceTests.Clock();
            using var native=FullVoiceTests.Native(speech,clock);native.Command("seek",300);
            var capture=new Capture();var callback=new Callback();
            using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(capture).BuildServiceProvider();
            await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
            var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<Story>(ParameterView.FromDictionary(new Dictionary<string,object?>
            {
                ["Match"]=native.Match,["Revision"]=1L,["Cutoff"]=300d,["Score"]=Replay.At(native.Match,300).Score,["Mode"]=mode,
                ["Listen"]=EventCallback.Factory.Create<(string Text,long Revision,double Cutoff)>(callback,
                    c=>native.ListenTo(c.Text,c.Revision,c.Cutoff)),
                ["Stop"]=EventCallback.Factory.Create(callback,native.StopListening)
            })));
            Assert.Contains("Listen",await renderer.Dispatcher.InvokeAsync(root.ToHtmlString));
            foreach(var chapter in native.Match.Narrative!.Chapters)
            {
                var count=speech.Text.Count+1;
                await renderer.Dispatcher.InvokeAsync(()=>capture.Story!.ListenChapterForTest(chapter.Id));
                await speech.WaitCount(count);
                Assert.Equal(mode==ExplanationMode.Analyst?chapter.Analyst:chapter.Fan,speech.Text.Last());
                speech.Complete();await speech.WaitIdle();
            }
            Assert.Equal(12,speech.Text.Count);
        }
        private sealed class Callback { }
        private sealed class Capture:IComponentActivator
        {
            public Studio? Studio;public Story? Story;
            public IComponent CreateInstance(Type t)
            {var c=(IComponent)Activator.CreateInstance(t)!;if(c is Studio s)Studio=s;if(c is Story st)Story=st;return c;}
        }
    }
}
namespace PitchLens.Core.Tests.Components
{
    public partial class MomentStudio
    {
        public Task ListenLeadForTest()=>Read(Claims[0].Text);
        public Task ListenClaimForTest(int index)=>Read(Claims[index].Text);
        public Task AudienceForVoiceTest(bool value)=>ChangeAudience(value);
    }
    public partial class NarrativeStory
    {
        public Task ListenChapterForTest(string id)=>ReadChapter(id);
    }
}
