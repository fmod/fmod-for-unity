using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class FMODEventPlayable : PlayableAsset, ITimelineClipAsset
{
    public FMODEventPlayableBehavior template = new FMODEventPlayableBehavior();

    public GameObject TrackTargetObject { get; set; }
    public float eventLength; //In seconds.

    FMODEventPlayableBehavior behavior;

    [FMODUnity.EventRef]
    [SerializeField] public string eventName;
    [SerializeField] public STOP_MODE stopType;

    [SerializeField] public FMODUnity.ParamRef[] parameters = new FMODUnity.ParamRef[0];

    [NonSerialized] public bool cachedParameters = false;

    public override double duration
    {
        get
        {
            if (eventName == null)
            {
                return base.duration;
            }
            else
            {
                return eventLength;
            }
        }
    }

    public ClipCaps clipCaps
    {
        get { return ClipCaps.None; }
    }

    public TimelineClip OwningClip { get; set; }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(eventName))
#else
        if (!cachedParameters && !string.IsNullOrEmpty(eventName))
#endif
        {
            FMOD.Studio.EventDescription eventDescription;
            FMODUnity.RuntimeManager.StudioSystem.getEvent(eventName, out eventDescription);
            for (int i = 0; i < parameters.Length; i++)
            {
                FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
                eventDescription.getParameterDescriptionByName(parameters[i].Name, out parameterDescription);
                parameters[i].ID = parameterDescription.id;
            }
            cachedParameters = true;
        }

        var playable = ScriptPlayable<FMODEventPlayableBehavior>.Create(graph, template);
        behavior = playable.GetBehaviour();

        behavior.TrackTargetObject = TrackTargetObject;
        behavior.eventName = eventName;
        behavior.stopType = stopType;
        behavior.parameters = parameters;
        behavior.OwningClip = OwningClip;

        return playable;
    }

#if UNITY_EDITOR
    public void UpdateEventDuration(float duration)
    {
        eventLength = duration / 1000f;
    }

    public void OnValidate()
    {
        if (OwningClip != null && !string.IsNullOrEmpty(eventName))
        {
            int index = eventName.LastIndexOf("/");
            OwningClip.displayName = eventName.Substring(index + 1);
        }
        if (behavior != null && !string.IsNullOrEmpty(behavior.eventName))
        {
            behavior.eventName = eventName;
        }
    }
#endif //UNITY_EDITOR
}

public enum STOP_MODE : int
{
    AllowFadeout,
    Immediate,
    None
}

public class FMODEventPlayableBehavior : PlayableBehaviour
{
    public string eventName;
    public STOP_MODE stopType = STOP_MODE.AllowFadeout;
    public FMODUnity.ParamRef[] parameters = new FMODUnity.ParamRef[0];

    public GameObject TrackTargetObject;
    public TimelineClip OwningClip;

    private bool isPlayheadInside = false;

    private FMOD.Studio.EventInstance eventInstance;

    protected void PlayEvent()
    {
        if (!string.IsNullOrEmpty(eventName))
        {
            eventInstance = FMODUnity.RuntimeManager.CreateInstance(eventName);
            // Only attach to object if the game is actually playing, not auditioning.
            if (Application.isPlaying && TrackTargetObject)
            {
                Rigidbody rb = TrackTargetObject.GetComponent<Rigidbody>();
                if (rb)
                {
                    FMODUnity.RuntimeManager.AttachInstanceToGameObject(eventInstance, TrackTargetObject.transform, rb);
                }
                else
                {
                    FMODUnity.RuntimeManager.AttachInstanceToGameObject(eventInstance, TrackTargetObject.transform, TrackTargetObject.GetComponent<Rigidbody2D>());
                }
            }
            else
            {
                eventInstance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(Vector3.zero));
            }

            foreach (var param in parameters)
            {
                eventInstance.setParameterByID(param.ID, param.Value);
            }

            eventInstance.start();
        }
    }

    public void OnEnter()
    {
        if (!isPlayheadInside)
        {
            PlayEvent();
            isPlayheadInside = true;
        }
    }

    public void OnExit()
    {
        if (isPlayheadInside)
        {
            if (eventInstance.isValid())
            {
                if (stopType != STOP_MODE.None)
                {
                    eventInstance.stop(stopType == STOP_MODE.Immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                }
                eventInstance.release();
            }
            isPlayheadInside = false;
        }
    }

    public void UpdateBehaviour(float time)
    {
        if ((time >= OwningClip.start) && (time < OwningClip.end))
        {
            OnEnter();
        }
        else
        {
            OnExit();
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        isPlayheadInside = false;
        if (eventInstance.isValid())
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
            FMODUnity.RuntimeManager.StudioSystem.update();
        }
    }
}