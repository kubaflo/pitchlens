namespace PitchLens.Core;

public interface ILocalSpeech
{
    Task SpeakAsync(string text, CancellationToken token);
}

public sealed class SpokenCommentary(ILocalSpeech engine) : IDisposable
{
    public static string? TextThatFits(string text,double wallSeconds)
    {
        if(!double.IsFinite(wallSeconds)||wallSeconds<=0)return null;
        var sentenceEnd=text.IndexOfAny(['.','!','?']);
        var first=sentenceEnd>=0?text[..(sentenceEnd+1)]:text;
        var limit=(int)Math.Floor(wallSeconds*2);
        if(text.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length<=limit)return text;
        return first.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length<=limit?first:null;
    }
    private readonly object gate = new();
    private readonly SemaphoreSlim serial = new(1, 1);
    private CancellationTokenSource? current;
    private string? lastKey;
    private long epoch;
    private bool disposed;
    private bool suppressNext;
    public bool Enabled { get; private set; }
    public string Status { get; private set; } = "Voice off";
    public event Action? Changed;
    public void SetEnabled(bool enabled)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            Enabled=enabled; StopCore(); lastKey=null;suppressNext=false;
            Status=enabled?"Voice ready · Short English cues · Goals only above1x":"Voice off";
        }
        Changed?.Invoke();
    }
    public void Stop()
    {
        lock(gate){if(disposed)return;StopCore();suppressNext=true;Status=Enabled?"Voice ready":"Voice off";}
        Changed?.Invoke();
    }
    private void StopCore(){epoch++;current?.Cancel();current=null;}
    public void Observe(string? key,string? text,bool playing)
    {
        CancellationTokenSource request;long version;
        lock(gate)
        {
            if(disposed||!Enabled)return;
            if(suppressNext){lastKey=key;suppressNext=false;return;}
            if(playing&&key is not null&&text is null)
            {
                if(lastKey==key)return;
                lastKey=key;StopCore();Status="Short voice calls · Long cue skipped";Changed?.Invoke();return;
            }
            if(!playing||key is null||text is null)
            {
                if(current is not null){StopCore();Status="Voice ready";Changed?.Invoke();}
                return;
            }
            if(key==lastKey)return;
            lastKey=key;StopCore();
            current=request=new();version=epoch;Status="Speaking · Local English voice";
        }
        Changed?.Invoke();
        _=SpeakAsync(text,request,version);
    }
    private async Task SpeakAsync(string text,CancellationTokenSource request,long version)
    {
        var entered=false;
        try
        {
            await serial.WaitAsync(request.Token).ConfigureAwait(false);entered=true;
            request.Token.ThrowIfCancellationRequested();
            await engine.SpeakAsync(text,request.Token).ConfigureAwait(false);
            lock(gate)if(!disposed&&version==epoch)Status="Voice ready";
        }
        catch(OperationCanceledException)when(request.IsCancellationRequested){}
        catch(Exception e)when(e is InvalidOperationException or NotSupportedException or IOException)
        {
            lock(gate)if(!disposed&&version==epoch){Enabled=false;Status=$"Voice unavailable: {e.Message} Football continues.";}
        }
        finally
        {
            if(entered)serial.Release();
            lock(gate)if(current==request)current=null;
            request.Dispose();Changed?.Invoke();
        }
    }
    public void Dispose()
    {
        lock(gate){if(disposed)return;StopCore();Enabled=false;disposed=true;Status="Voice off";}
    }
}
