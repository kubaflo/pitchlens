namespace PitchLens.Core;

public interface ILocalSpeech
{
    Task SpeakAsync(string text, CancellationToken token);
}

public sealed class SpokenCommentary(ILocalSpeech engine) : IDisposable
{
    private readonly object gate = new();
    private readonly SemaphoreSlim serial = new(1,1);
    private CancellationTokenSource? current;
    private long epoch;
    private bool disposed;
    private string? currentKey,completedKey;
    public bool Enabled { get; private set; }
    public bool Manual { get; private set; }
    public string? ReadingText { get; private set; }
    public string Status { get; private set; }="Voice off";
    public event Action? Changed;
    public bool Completed(string key){lock(gate)return completedKey==key;}
    public void SetEnabled(bool enabled)
    {
        lock(gate){ObjectDisposedException.ThrowIf(disposed,this);StopCore();Enabled=enabled;Status=enabled?"Voice-paced · Full comments":"Voice off";}
        Changed?.Invoke();
    }
    public void Stop()
    {
        lock(gate){if(disposed)return;StopCore();Status=Enabled?"Voice-paced · Ready":"Voice off";}
        Changed?.Invoke();
    }
    private void StopCore(){epoch++;current?.Cancel();current=null;currentKey=completedKey=null;Manual=false;ReadingText=null;}
    public void Read(string key,string text,bool manual=false)
    {
        CancellationTokenSource request;long version;
        lock(gate)
        {
            if(disposed||(!Enabled&&!manual)||currentKey==key||completedKey==key)return;
            StopCore();current=request=new();currentKey=key;Manual=manual;ReadingText=text;version=epoch;Status="Speaking full comment · Local English voice";
        }
        Changed?.Invoke();_=SpeakAsync(key,text,request,version);
    }
    private async Task SpeakAsync(string key,string text,CancellationTokenSource request,long version)
    {
        var entered=false;var notify=false;
        try
        {
            await serial.WaitAsync(request.Token).ConfigureAwait(false);entered=true;
            request.Token.ThrowIfCancellationRequested();
            await engine.SpeakAsync(text,request.Token).ConfigureAwait(false);
            lock(gate)if(!disposed&&version==epoch){completedKey=key;current=null;Manual=false;ReadingText=null;Status=Enabled?"Voice-paced · Ready":"Voice finished";notify=true;}
        }
        catch(OperationCanceledException)when(request.IsCancellationRequested){}
        catch(Exception e)when(e is InvalidOperationException or NotSupportedException or IOException)
        {lock(gate)if(!disposed&&version==epoch){Enabled=false;Manual=false;ReadingText=null;current=null;Status=$"Voice unavailable: {e.Message} Football continues.";notify=true;}}
        finally
        {
            if(entered)serial.Release();
            request.Dispose();
            if(notify)Changed?.Invoke();
        }
    }
    public void Dispose(){lock(gate){if(disposed)return;StopCore();Enabled=false;disposed=true;Status="Voice off";}}
}

public sealed record SpeechBeat(string Key,double Start,double End,string Text);

public sealed class SpeechPacing(IReadOnlyList<SpeechBeat> beats)
{
    private int index;
    private bool started;
    public string? HeldKey { get; private set; }
    public void Reset(double time,bool skipCurrent)
    {
        index=0;started=false;HeldKey=null;
        while(index<beats.Count&&(beats[index].End<=time || skipCurrent&&beats[index].Start<=time))index++;
    }
    public double? Update(double time,bool playing,SpokenCommentary voice)
    {
        HeldKey=null;
        if(!voice.Enabled||voice.Manual)return null;
        while(index<beats.Count)
        {
            var beat=beats[index];
            if(!started&&time<beat.Start)return beat.Start;
            if(!started)
            {
                if(!playing)return Math.Max(time,beat.Start);
                started=true;voice.Read(beat.Key,beat.Text);
            }
            if(!voice.Enabled)return null;
            if(voice.Completed(beat.Key)){index++;started=false;continue;}
            // Keep the end-exclusive caption/film scene visible during its listening hold.
            var boundary=Math.Max(time,Math.BitDecrement(beat.End));
            if(time>=Math.BitDecrement(beat.End))HeldKey=beat.Key;
            return boundary;
        }
        return null;
    }
}
