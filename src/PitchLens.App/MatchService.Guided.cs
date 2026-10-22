using PitchLens.Generation;

namespace PitchLens.App;

public sealed partial class MatchService
{
    public int? GuidedDemoStep { get; private set; }
    public bool GuidedDemoActive=>GuidedDemoStep is not null;
    public bool CanStartGuidedDemo=>!disposed&&!GuidedDemoActive&&!Generating&&!CheckingConnection&&!Explainer.Busy&&!Director.Busy&&!FilmActive&&
        (Mode==SessionMode.Unchosen&&!HasMatch||Mode==SessionMode.Test&&!Fresh&&
            MomentRecording.Available(presentation.Match,presentation.Revision));
    public void StartGuidedDemo()
    {
        lock(gate)
        {
            if(!CanStartGuidedDemo)throw new InvalidOperationException("Guided demo requires Test mode and the matching recording. Your current game and Live preference are kept.");
            if(Mode==SessionMode.Unchosen)
            {
                // This explicit first-choice button says it starts Test mode; never replace a remembered Live choice.
                saveMode?.Invoke(SessionMode.Test);Mode=SessionMode.Test;
            }
            PlayRecordedDemo();GuidedDemoStep=0;SetGuidedDemoStep(0);
        }
    }
    public void SetGuidedDemoStep(int step)
    {
        lock(gate)
        {
            if(!GuidedDemoActive||step is <0 or >3||Mode!=SessionMode.Test||Fresh||
                Generating||CheckingConnection||Explainer.Busy||Director.Busy||
                !MomentRecording.Available(presentation.Match,presentation.Revision))
                throw new InvalidOperationException("Guided step requires the idle, matching recorded Test scene.");
            film=null;presentation.Pause();
            if(step==0)presentation.Seek(76);
            else if(step is 1 or 2){presentation.Seek(86);TestExplain();}
            else{presentation.Seek(300);PreviewRecordedDirector();}
            GuidedDemoStep=step;
        }
        NotifyChanged();
    }
    public void ExitGuidedDemo()
    {
        lock(gate){film=null;presentation.Pause();GuidedDemoStep=null;}
        NotifyChanged();
    }
}
