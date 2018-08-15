#if UNITY_2017_1_OR_NEWER

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
#endif

[TrackColor(0.066f, 0.134f, 0.244f)]
[TrackClipType(typeof(FMODEventPlayable))]
[TrackBindingType(typeof(GameObject))]
public class FMODEventTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var director = go.GetComponent<PlayableDirector>();
        var trackTargetObject = director.GetGenericBinding(this) as GameObject;

        foreach (var clip in GetClips())
        {
            var playableAsset = clip.asset as FMODEventPlayable;

            if (playableAsset)
            {
                playableAsset.TrackTargetObject = trackTargetObject;
                playableAsset.OwningClip = clip;
            }
        }

        var scriptPlayable = ScriptPlayable<FMODEventMixerBehaviour>.Create(graph, inputCount);
        return scriptPlayable;
    }
}

public class FMODEventMixerBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
#if UNITY_EDITOR
        /*
         * Process frame is called from OnGUI() when auditioning.
         * Check playing to avoid retriggering sounds while scrubbing or repainting.
         * Check IsQuitting to avoid accessing the RuntimeManager during the Play-In-Editor to Editor transition.
         */
        bool playing = playable.GetGraph().IsPlaying();
        if (!playing || FMODUnity.RuntimeManager.IsQuitting())
        {
            return;
        }
        /* When auditioning manually update the StudioSystem in place of the RuntimeManager. */
        if (!Application.isPlaying)
        {
            FMODUnity.RuntimeManager.StudioSystem.update();
        }
#endif //UNITY_EDITOR

        int inputCount = playable.GetInputCount();
        float time = (float)playable.GetGraph().GetRootPlayable(0).GetTime();

        for (int i = 0; i < inputCount; i++)
        {
            ScriptPlayable<FMODEventPlayableBehavior> inputPlayable = (ScriptPlayable<FMODEventPlayableBehavior>)playable.GetInput(i);
            FMODEventPlayableBehavior input = inputPlayable.GetBehaviour();

            input.UpdateBehaviour(time);
        }
    }
}

#endif //UNITY_2017_1_OR_NEWER