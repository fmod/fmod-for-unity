using UnityEngine;
using System;
using System.Collections.Generic;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Event Emitter")]
    public class StudioEventEmitter : MonoBehaviour
    {
        [EventRef]
        public String Event;
        public EmitterGameEvent PlayEvent;
        public EmitterGameEvent StopEvent;
        public String CollisionTag;
        public bool AllowFadeout = true;
        public bool TriggerOnce = false;

        public ParamRef[] Params;
        
        private FMOD.Studio.EventDescription eventDescription;
        private FMOD.Studio.EventInstance instance;
        private bool hasTriggered;
        private Rigidbody cachedRigidBody;
        private bool isOneshot;
        private bool isQuitting;

        void Start() 
        {
            RuntimeUtils.EnforceLibraryOrder();
            cachedRigidBody = GetComponent<Rigidbody>();
            enabled = false;
            HandleGameEvent(EmitterGameEvent.LevelStart);
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        void OnDestroy()
        {
            if (!isQuitting)
            {
                HandleGameEvent(EmitterGameEvent.LevelEnd);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (String.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerEnter);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (String.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerExit);
            }
        }

        void OnCollisionEnter()
        {
            HandleGameEvent(EmitterGameEvent.CollisionEnter);
        }

        void OnCollisionExit()
        {
            HandleGameEvent(EmitterGameEvent.CollisionExit);
        }

        void HandleGameEvent(EmitterGameEvent gameEvent)
        {
            if (PlayEvent == gameEvent)
            {
                Play();
            }
            if (StopEvent == gameEvent)
            {
                Stop();
            }
        }

        void Lookup()
        {
            eventDescription = RuntimeManager.GetEventDescription(Event);
        }

        public void Play()
        {
            if (TriggerOnce && hasTriggered)
            {
                return;
            }

            if (String.IsNullOrEmpty(Event))
            {
                return;
            }

            if (eventDescription == null)
            {
                Lookup();
                eventDescription.isOneshot(out isOneshot);
            }

            // Let previous oneshot instances play out
            if (isOneshot && instance != null)
            {
                instance.release();
                instance = null;
            }

            if (instance == null)
            {
                eventDescription.createInstance(out instance);
            }

            instance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, cachedRigidBody));
            foreach(var param in Params)
            {
                instance.setParameterValue(param.Name, param.Value);
            }
            instance.start();

            hasTriggered = true;

            // Only want to update if we need to set 3D attributes
            bool is3d = false;
            eventDescription.is3D(out is3d);
            if (is3d)
            {
                enabled = true;
            }
        }

        public void Stop()
        {
            if (instance != null)
            {
                instance.stop(AllowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
                instance = null;
            }
            enabled = false;
        }

        void Update()
        {
            if (instance != null)
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject, cachedRigidBody));
                FMOD.Studio.PLAYBACK_STATE state;
                instance.getPlaybackState(out state);
                if (state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
                {
                    instance.release();
                    instance = null;
                    enabled = false;
                }
            }
        }

        public void SetParameter(string name, float value)
        {
            if (instance != null)
            {
                instance.setParameterValue(name, value);
            }
        }        
    }
}
