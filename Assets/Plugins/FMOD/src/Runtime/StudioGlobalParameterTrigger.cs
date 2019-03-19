using System;
using UnityEngine;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Global Parameter Trigger")]
    public class StudioGlobalParameterTrigger: MonoBehaviour
    {
        [ParamRef]
        public string parameter;
        public EmitterGameEvent TriggerEvent;
        public string CollisionTag;
        public float value;

        private FMOD.Studio.PARAMETER_DESCRIPTION parameterDescription;
        public FMOD.Studio.PARAMETER_DESCRIPTION ParameterDesctription { get { return parameterDescription; } }

        FMOD.RESULT Lookup()
        {
            FMOD.RESULT result = RuntimeManager.StudioSystem.getParameterDescriptionByName(parameter, out parameterDescription);
            return result;
        }

        void Awake()
        {
            if (string.IsNullOrEmpty(parameterDescription.name))
            {
                Lookup();
            }
        }

        void Start()
        {
            HandleGameEvent(EmitterGameEvent.ObjectStart);
        }

        void OnDestroy()
        {
            HandleGameEvent(EmitterGameEvent.ObjectDestroy);
        }

        void OnEnable()
        {
            HandleGameEvent(EmitterGameEvent.ObjectEnable);
        }

        void OnDisable()
        {
            HandleGameEvent(EmitterGameEvent.ObjectDisable);
        }

        void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerEnter);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerExit);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerEnter2D);
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(EmitterGameEvent.TriggerExit2D);
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

        void OnCollisionEnter2D()
        {
            HandleGameEvent(EmitterGameEvent.CollisionEnter2D);
        }

        void OnCollisionExit2D()
        {
            HandleGameEvent(EmitterGameEvent.CollisionExit2D);
        }

        void HandleGameEvent(EmitterGameEvent gameEvent)
        {
            if (TriggerEvent == gameEvent)
            {
                TriggerParameters();
            }
        }

        public void TriggerParameters()
        {
            if (!string.IsNullOrEmpty(parameter))
            {
                FMOD.RESULT result = RuntimeManager.StudioSystem.setParameterByID(parameterDescription.id, value);
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogError(string.Format(("[FMOD] StudioGlobalParameterTrigger failed to set parameter {0} : result = {1}"), parameter, result));
                }
            }
        }
    }
}