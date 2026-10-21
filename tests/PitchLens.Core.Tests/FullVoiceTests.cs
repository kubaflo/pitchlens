using PitchLens.App;
using PitchLens.Core;
using PitchLens.Generation;

namespace PitchLens.Core.Tests;

public sealed class FullVoiceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Continuous_play_reads_all_forty_exact_full_cues_once_in_order_even_with_huge_clock_jumps(double rate)
    {
        var clock=new Clock();var speech=new ControlledSpeech();
        using var native=Native(speech,clock);
        native.Command("rate",rate);native.SetVoiceEnabled(true);native.Command("play");
        var expected=native.Match.Commentary!.Cues;
        for(var i=0;i<expected.Count;i++)
        {
            clock.Seconds+=1000;
            var arrived=native.Sample(true)!.Frame;
            await speech.WaitCount(i+1);
            Assert.Equal(expected[i].Start,arrived.Snapshot.Time,7);
            Assert.Equal(expected[i].Text,speech.Text[i]);
            Assert.True(arrived.Playing);Assert.True(arrived.VoicePaced);Assert.Equal(1,arrived.EffectiveRate);
            clock.Seconds+=1000;
            var held=native.Sample(true)!.Frame;
            Assert.True(held.Snapshot.Time<expected[i].End);
            Assert.Equal(expected[i].Id,CommentaryReplay.At(native.Match.Commentary,held.Snapshot.Time)!.Id);
            Assert.True(native.ListeningHold);
            for(var j=0;j<4;j++)native.Sample(true);
            Assert.Equal(i+1,speech.Text.Count);Assert.Equal(1,speech.MaxConcurrent);
            speech.Complete();
            await speech.WaitIdle();
            native.Sample(true);
        }
        clock.Seconds+=1000;var end=native.Sample(true)!.Frame;
        Assert.Equal(300,end.Snapshot.Time);Assert.False(end.Playing);
        Assert.Equal(expected.Select(c=>c.Text),speech.Text);
        native.SetVoiceEnabled(false);Assert.Equal(rate,native.Sample(true)!.Frame.Rate);
        Assert.False(native.Sample(true)!.Frame.VoicePaced);
    }

    [Fact]
    public async Task All_four_full_director_holds_extend_presentation_not_edit_and_release_last_end_only_after_completion()
    {
        var clock=new Clock();var speech=new ControlledSpeech();
        using var native=Native(speech,clock);
        native.Command("seek",300);native.PreviewRecordedDirector();
        native.SetVoiceEnabled(true);
        var scenes=native.Director.Current!.Plan.Scenes;
        var plan=native.Director.Current.Plan;
        double offset=0;
        for(var i=0;i<scenes.Length;i++)
        {
            clock.Seconds+=1000;native.Sample(true);await speech.WaitCount(i+1);
            var start=offset+(scenes[i].SourceEnd-scenes[i].SourceStart)/scenes[i].Rate;
            Assert.Equal(start,native.FilmFrame!.FilmTime,7);
            Assert.Equal(scenes[i].SourceEnd,native.Sample(true)!.Frame.Snapshot.Time);
            Assert.Equal(scenes[i].Text,speech.Text[i]);
            clock.Seconds+=1000;native.Sample(true);
            Assert.True(native.FilmPlaying);Assert.True(native.ListeningHold);
            Assert.True(native.FilmFrame.FilmTime<offset+FilmTimeline.SceneDuration(scenes[i]));
            speech.Complete();await speech.WaitIdle();native.Sample(true);
            offset+=FilmTimeline.SceneDuration(scenes[i]);
        }
        clock.Seconds+=1000;native.Sample(true);
        Assert.Equal(58,native.FilmFrame!.FilmTime);Assert.False(native.FilmPlaying);
        Assert.Equal(58,FilmTimeline.Duration(plan));Assert.Same(plan,native.Director.Current!.Plan);
        native.FilmCommand("exit");Assert.Equal(300,native.Sample(true)!.Frame.Snapshot.Time);
    }

    [Fact]
    public async Task Early_completion_still_plays_source_footage_and_seek_pause_off_never_catch_up_or_resume_user_pause()
    {
        var clock=new Clock();var speech=new ControlledSpeech();
        using var native=Native(speech,clock);
        native.Command("rate",3);native.SetVoiceEnabled(true);native.Command("play");
        clock.Seconds=3;native.Sample(true);await speech.WaitCount(1);
        speech.Complete();await speech.WaitIdle();native.Sample(true);
        clock.Seconds=4;Assert.Equal(4,native.Sample(true)!.Frame.Snapshot.Time);
        native.Command("seek",86);native.Sample(true);
        native.Command("play");native.Sample(true);
        Assert.Single(speech.Text); // Seek destination is intentionally not announced.
        native.Command("pause");speech.CompleteIfAny();await speech.WaitIdle();
        clock.Seconds=500;Assert.Equal(86,native.Sample(true)!.Frame.Snapshot.Time);
        Assert.False(native.Sample(true)!.Frame.Playing);
        native.SetVoiceEnabled(false);native.Command("play");clock.Seconds=501;
        Assert.Equal(87,native.Sample(true)!.Frame.Snapshot.Time); // Highlight1x still applies at this point.
    }
    [Fact]
    public async Task Normal_quarter_second_samples_with_early_engine_completions_preserve_all_forty_in_order()
    {
        var clock=new Clock();var speech=new ControlledSpeech();using var native=Native(speech,clock);
        native.Command("rate",5);native.SetVoiceEnabled(true);native.Command("play");
        var completed=0;
        for(var frame=0;frame<2000&&native.Sample(true)!.Frame.Playing;frame++)
        {
            clock.Seconds+=.25;var sampled=native.Sample(true)!.Frame;
            Assert.All(sampled.Snapshot.Events,e=>Assert.True(e.Time<=sampled.Snapshot.Time));
            if(speech.Text.Count>completed)
            {speech.Complete();await speech.WaitIdle();completed=speech.Text.Count;native.Sample(true);}
        }
        Assert.Equal(40,speech.Text.Count);
        Assert.Equal(native.Match.Commentary!.Cues.Select(c=>c.Text),speech.Text);
        Assert.Equal(300,native.Sample(true)!.Frame.Snapshot.Time);
    }

    [Fact]
    public async Task Manual_full_paragraph_works_with_auto_voice_off_pauses_and_is_guarded_by_scene_and_cutoff()
    {
        var speech=new ControlledSpeech();var clock=new Clock();using var native=Native(speech,clock);
        native.Command("seek",86);native.Command("play");
        var text=MomentRecording.Load(native.Match,1,86).Answer.Analyst[0].Text;
        native.ListenTo(text,1,86);await speech.WaitCount(1);
        Assert.Equal(text,speech.Text[0]);Assert.True(native.Voice.Manual);Assert.False(native.Voice.Enabled);
        Assert.False(native.Sample(true)!.Frame.Playing);
        speech.Complete();await speech.WaitIdle();Assert.False(native.Sample(true)!.Frame.Playing);
        Assert.Throws<InvalidOperationException>(()=>native.ListenTo(text,2,86));
        Assert.Throws<InvalidOperationException>(()=>native.ListenTo(text,1,100));
        native.ListenTo(text,1,86);await speech.WaitCount(2);native.StopManualListening();await speech.WaitIdle();
        Assert.False(native.Voice.Manual);
    }

    [Fact]
    public async Task Voice_off_pause_scene_cancel_and_error_release_only_owned_hold_and_never_resume_user_pause()
    {
        var clock=new Clock();var speech=new ControlledSpeech();using var native=Native(speech,clock);
        native.Command("rate",5);native.SetVoiceEnabled(true);native.Command("play");
        clock.Seconds=1000;native.Sample(true);await speech.WaitCount(1);
        clock.Seconds=2000;native.Sample(true);Assert.True(native.ListeningHold);
        var held=native.Sample(true)!.Frame.Snapshot.Time;
        native.SetVoiceEnabled(false);await speech.WaitIdle();
        Assert.Equal(held,native.Sample(true)!.Frame.Snapshot.Time);
        clock.Seconds+=1;Assert.Equal(held+5,native.Sample(true)!.Frame.Snapshot.Time,8);
        native.SetVoiceEnabled(true);native.Command("seek",0);native.Command("restart");native.Command("play");
        clock.Seconds+=1000;native.Sample(true);await speech.WaitCount(2);
        native.Command("pause");await speech.WaitIdle();clock.Seconds+=1000;
        Assert.False(native.Sample(true)!.Frame.Playing);
        native.SetVoiceEnabled(false);Assert.False(native.Sample(true)!.Frame.Playing);
        native.Command("restart");native.SetVoiceEnabled(true);native.Command("play");
        clock.Seconds+=1000;native.Sample(true);await speech.WaitCount(3);
        native.Suspend();await speech.WaitIdle();Assert.False(native.Sample(true)!.Frame.Playing);
        native.Dispose();Assert.Equal("Voice off",native.Voice.Status);

        var failureClock=new Clock();using var failed=Native(new ErrorSpeech(),failureClock);
        failed.Command("rate",3);failed.SetVoiceEnabled(true);failed.Command("play");
        failureClock.Seconds=1000;failed.Sample(true);await Task.Delay(10);
        Assert.False(failed.Voice.Enabled);Assert.Contains("Voice unavailable",failed.Voice.Status);
        var before=failed.Sample(true)!.Frame.Snapshot.Time;failureClock.Seconds++;
        Assert.Equal(before+3,failed.Sample(true)!.Frame.Snapshot.Time,8);
        Assert.True(failed.Sample(true)!.Frame.Playing);
    }
    [Fact]
    public async Task Stale_engine_success_after_seek_cannot_release_a_newer_comment_hold()
    {
        var clock=new Clock();var speech=new IgnoreCancellationSpeech();using var native=Native(speech,clock);
        native.SetVoiceEnabled(true);native.Command("play");clock.Seconds=1000;native.Sample(true);
        Assert.Single(speech.Text);
        native.Command("seek",86);native.Sample(true); // Skip the destination goal, retire the old engine task.
        clock.Seconds+=1000;native.Sample(true);
        var next=native.Match.Commentary!.Cues.First(c=>c.Start>86);
        Assert.Equal(next.Start,native.Sample(true)!.Frame.Snapshot.Time,8);
        clock.Seconds+=1000;native.Sample(true);
        Assert.True(native.ListeningHold);
        var held=native.Sample(true)!.Frame.Snapshot.Time;
        speech.Completions[0].SetResult();
        for(var i=0;i<100&&speech.Text.Count<2;i++)await Task.Delay(2);
        Assert.Equal(2,speech.Text.Count);Assert.Equal(next.Text,speech.Text[1]);
        native.Sample(true);Assert.Equal(held,native.Sample(true)!.Frame.Snapshot.Time);Assert.True(native.ListeningHold);
        speech.Completions[1].SetResult();await Task.Delay(10);native.Sample(true);
        Assert.False(native.ListeningHold);
    }
    private sealed class IgnoreCancellationSpeech:ILocalSpeech
    {
        public List<string> Text=[];public List<TaskCompletionSource> Completions=[];
        public Task SpeakAsync(string text,CancellationToken token)
        {Text.Add(text);var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);Completions.Add(completion);return completion.Task;}
    }
    private sealed class ErrorSpeech:ILocalSpeech
    {public Task SpeakAsync(string text,CancellationToken token)=>throw new NotSupportedException("Controlled English engine failure");}

    internal static MatchService Native(ILocalSpeech speech,Clock clock)=>new(new Forbidden(),new Forbidden(),
        (_,_)=>throw new InvalidOperationException("No SDK"),speech:speech,replayClock:clock,filmClock:clock,mode:SessionMode.Test);
    internal sealed class Clock:IMonotonicClock{public double Seconds{get;set;}}
    internal sealed class ControlledSpeech:ILocalSpeech
    {
        public List<string> Text=[];private TaskCompletionSource? current;private int active;
        public int MaxConcurrent;
        public async Task SpeakAsync(string text,CancellationToken token)
        {
            active++;MaxConcurrent=Math.Max(MaxConcurrent,active);Text.Add(text);
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);current=completion;
            try{await completion.Task.WaitAsync(token);}
            finally{active--;if(current==completion)current=null;}
        }
        public void Complete()=>current!.TrySetResult();
        public void CompleteIfAny()=>current?.TrySetResult();
        public async Task WaitCount(int n){for(var i=0;i<200&&Text.Count<n;i++)await Task.Delay(2);Assert.Equal(n,Text.Count);}
        public async Task WaitIdle(){for(var i=0;i<200&&active!=0;i++)await Task.Delay(2);await Task.Delay(5);Assert.Equal(0,active);}
    }
    private sealed class Forbidden:IScenarioSource,IMomentSource
    {
        public Task<string> GenerateAsync(IProgress<string>?p,CancellationToken t)=>throw new InvalidOperationException("No SDK");
        public Task<string> ExplainAsync(MomentEvidence e,CancellationToken t)=>throw new InvalidOperationException("No SDK");
    }
}
