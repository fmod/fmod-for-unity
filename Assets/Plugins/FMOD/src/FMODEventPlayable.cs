#if UNITY_TIMELINE_EXIST

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Serialization;

namespace FMODUnity
{
    [System.Serializable]
    public class FMODEventPlayable : PlayableAsset, ITimelineClipAsset
    {
        [FormerlySerializedAs("template")]
        public FMODEventPlayableBehavior Template = new FMODEventPlayableBehavior();

        [FormerlySerializedAs("eventLength")]
        public float EventLength; //In seconds.

        [Obsolete("Use the eventReference field instead")]
        [SerializeField]
        public string eventName;

        [FormerlySerializedAs("eventReference")]
        [SerializeField]
        public EventReference EventReference;

        [FormerlySerializedAs("stopType")]
        [SerializeField]
        public STOP_MODE StopType;

        [FormerlySerializedAs("parameters")]
        [SerializeField]
        public ParamRef[] Parameters = new ParamRef[0];

        [NonSerialized]
        public bool CachedParameters = false;

        private FMODEventPlayableBehavior behavior;

        public GameObject TrackTargetObject { get; set; }

        public override double duration
        {
            get
            {
                if (EventReference.IsNull)
                {
                    return base.duration;
                }
                else
                {
                    return EventLength;
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
            if (!EventReference.IsNull)
#else
            if (!CachedParameters && !EventReference.IsNull)
#endif
            {
                FMOD.Studio.EventDescription eventDescription = RuntimeManager.GetEventDescription(EventReference);

                for (int i = 0; i < Parameters.Length; i++)
                {
                    FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
                    eventDescription.getParameterDescriptionByName(Parameters[i].Name, out parameterDescription);
                    Parameters[i].ID = parameterDescription.id;
                }

                List<ParameterAutomationLink> parameterLinks = Template.ParameterLinks;

                for (int i = 0; i < parameterLinks.Count; i++)
                {
                    FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
                    eventDescription.getParameterDescriptionByName(parameterLinks[i].Name, out parameterDescription);
                    parameterLinks[i].ID = parameterDescription.id;
                }

                CachedParameters = true;
            }

            var playable = ScriptPlayable<FMODEventPlayableBehavior>.Create(graph, Template);
            behavior = playable.GetBehaviour();

            behavior.TrackTargetObject = TrackTargetObject;
            behavior.EventReference = EventReference;
            behavior.StopType = StopType;
            behavior.Parameters = Parameters;
            behavior.OwningClip = OwningClip;

            return playable;
        }

#if UNITY_EDITOR
        public void UpdateEventDuration(float duration)
        {
            EventLength = duration / 1000f;
        }

        public void OnValidate()
        {
            if (OwningClip != null && !EventReference.IsNull)
            {
                int index = EventReference.Path.LastIndexOf("/");
                OwningClip.displayName = EventReference.Path.Substring(index + 1);
            }
            if (behavior != null && !behavior.EventReference.IsNull)
            {
                behavior.EventReference = EventReference;
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
        [FormerlySerializedAs("eventReference")]
        public EventReference EventReference;

        [FormerlySerializedAs("stopType")]
        public STOP_MODE StopType = STOP_MODE.AllowFadeout;

        [FormerlySerializedAs("parameters")]
        [NotKeyable]
        public ParamRef[] Parameters = new ParamRef[0];

        [FormerlySerializedAs("parameterLinks")]
        public List<ParameterAutomationLink> ParameterLinks = new List<ParameterAutomationLink>();

        [NonSerialized]
        public GameObject TrackTargetObject;

        [NonSerialized]
        public TimelineClip OwningClip;

        [FormerlySerializedAs("parameterAutomation")]
        public AutomatableSlots ParameterAutomation;

        private bool isPlayheadInside = false;

        private FMOD.Studio.EventInstance eventInstance;
        private float currentVolume = 1;

        protected void PlayEvent()
        {
            if (!EventReference.IsNull)
            {
                eventInstance = RuntimeManager.CreateInstance(EventReference);

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

                foreach (var param in Parameters)
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
                    if (StopType != STOP_MODE.None)
                    {
                        eventInstance.stop(StopType == STOP_MODE.Immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
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
                foreach (ParameterAutomationLink link in ParameterLinks)
                {
                    float value = ParameterAutomation.GetValue(link.Slot);
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