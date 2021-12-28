#if UNITY_TIMELINE_EXIST

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FMODUnity
{
    [System.Serializable]
    public class FMODEventPlayable : PlayableAsset, ITimelineClipAsset
    {
        public FMODEventPlayableBehavior template = new FMODEventPlayableBehavior();

        public float eventLength; //In seconds.

        [Obsolete("Use the eventReference field instead")]
        [SerializeField]
        public string eventName;

        [SerializeField]
        public EventReference eventReference;

        [SerializeField]
        public STOP_MODE stopType;

        [SerializeField]
        public ParamRef[] parameters = new ParamRef[0];

        [NonSerialized]
        public bool cachedParameters = false;

        FMODEventPlayableBehavior behavior;

        public GameObject TrackTargetObject { get; set; }

        public override double duration
        {
            get
            {
                if (eventReference.IsNull)
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
            if (!eventReference.IsNull)
#else
            if (!cachedParameters && !eventReference.IsNull)
#endif
            {
                FMOD.Studio.EventDescription eventDescription = RuntimeManager.GetEventDescription(eventReference);

                for (int i = 0; i < parameters.Length; i++)
                {
                    FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
                    eventDescription.getParameterDescriptionByName(parameters[i].Name, out parameterDescription);
                    parameters[i].ID = parameterDescription.id;
                }

                List<ParameterAutomationLink> parameterLinks = template.parameterLinks;

                for (int i = 0; i < parameterLinks.Count; i++)
                {
                    FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
                    eventDescription.getParameterDescriptionByName(parameterLinks[i].Name, out parameterDescription);
                    parameterLinks[i].ID = parameterDescription.id;
                }

                cachedParameters = true;
            }

            var playable = ScriptPlayable<FMODEventPlayableBehavior>.Create(graph, template);
            behavior = playable.GetBehaviour();

            behavior.TrackTargetObject = TrackTargetObject;
            behavior.eventReference = eventReference;
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
            if (OwningClip != null && !eventReference.IsNull)
            {
                int index = eventReference.Path.LastIndexOf("/");
                OwningClip.displayName = eventReference.Path.Substring(index + 1);
            }
            if (behavior != null && !behavior.eventReference.IsNull)
            {
                behavior.eventReference = eventReference;
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

    [Serializable]
    public class ParameterAutomationLink
    {
        public string Name;
        public FMOD.Studio.PARAMETER_ID ID;
        public int Slot;
    }

    [Serializable]
    public class FMODEventPlayableBehavior : PlayableBehaviour
    {
        public EventReference eventReference;
        public STOP_MODE stopType = STOP_MODE.AllowFadeout;
        [NotKeyable]
        public ParamRef[] parameters = new ParamRef[0];
        public List<ParameterAutomationLink> parameterLinks = new List<ParameterAutomationLink>();

        [NonSerialized]
        public GameObject TrackTargetObject;

        [NonSerialized]
        public TimelineClip OwningClip;

        public AutomatableSlots parameterAutomation;

        private bool isPlayheadInside = false;

        private FMOD.Studio.EventInstance eventInstance;
        private float currentVolume = 1;

        protected void PlayEvent()
        {
            if (!eventReference.IsNull)
            {
                eventInstance = RuntimeManager.CreateInstance(eventReference);

                // Only attach to object if the game is actually playing, not auditioning.
                if (Application.isPlaying && TrackTargetObject)
                {
#if UNITY_PHYSICS_EXIST
                    if (TrackTargetObject.GetComponent<Rigidbody>())
                    {
                        RuntimeManager.AttachInstanceToGameObject(eventInstance, TrackTargetObject.transform, TrackTargetObject.GetComponent<Rigidbody>());
                    }
                    else
#endif
#if UNITY_PHYSICS2D_EXIST
                    if (TrackTargetObject.GetComponent<Rigidbody2D>())
                    {
                        RuntimeManager.AttachInstanceToGameObject(eventInstance, TrackTargetObject.transform, TrackTargetObject.GetComponent<Rigidbody2D>());
                    }
                    else
#endif
                    {
                        RuntimeManager.AttachInstanceToGameObject(eventInstance, TrackTargetObject.transform);
                    }
                }
                else
                {
                    eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(Vector3.zero));
                }

                foreach (var param in parameters)
                {
                    eventInstance.setParameterByID(param.ID, param.Value);
                }

                eventInstance.setVolume(currentVolume);
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

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (eventInstance.isValid())
            {
                foreach (ParameterAutomationLink link in parameterLinks)
                {
                    float value = parameterAutomation.GetValue(link.Slot);
                    eventInstance.setParameterByID(link.ID, value);
                }
            }
        }

        public void UpdateBehavior(float time, float volume)
        {
            if (volume != currentVolume)
            {
                currentVolume = volume;

                if (eventInstance.isValid())
                {
                    eventInstance.setVolume(volume);
                }
            }

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
                RuntimeManager.StudioSystem.update();
            }
        }
    }
}
#endif