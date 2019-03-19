using System;
using UnityEngine;

namespace FMODUnity
{
    [Serializable]
    public class EmitterRef
    {
        public StudioEventEmitter Target;
        public ParamRef[] Params;
    }

    [AddComponentMenu("FMOD Studio/FMOD Studio Parameter Trigger")]
    public class StudioParameterTrigger: MonoBehaviour
    {
        public EmitterRef[] Emitters;
        public EmitterGameEvent TriggerEvent;
        public string CollisionTag;

        void Awake()
        {
            for (int i = 0; i < Emitters.Length; i++)
            {
                var emitterRef = Emitters[i];
                if (emitterRef.Target != null && !string.IsNullOrEmpty(emitterRef.Target.Event))
                {
                    FMOD.Studio.EventDescription eventDesc = FMODUnity.RuntimeManager.GetEventDescription(emitterRef.Target.Event);
                    if (eventDesc.isValid())
                    {
                        for (int j = 0; j < Emitters[i].Params.Length; j++)
                        {
                            FMOD.Studio.PARAMETER_DESCRIPTION param;
                            eventDesc.getParameterDescriptionByName(emitterRef.Target.Params[j].Name, out param);
                            emitterRef.Params[j].ID = param.id;
                        }
                    }
                }
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
            for (int i = 0; i < Emitters.Length; i++)
            {
                var emitterRef = Emitters[i];
                if (emitterRef.Target != null && emitterRef.Target.EventInstance.isValid())
                {
                    for (int j = 0; j < Emitters[i].Params.Length; j++)
                    {
                        emitterRef.Target.EventInstance.setParameterByID(Emitters[i].Params[j].ID, Emitters[i].Params[j].Value);
                    }
                }
            }
        }
    }
}