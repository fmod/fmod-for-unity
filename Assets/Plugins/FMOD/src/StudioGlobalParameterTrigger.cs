using System;
using UnityEngine;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Global Parameter Trigger")]
    public class StudioGlobalParameterTrigger: EventHandler
    {
        [ParamRef]
        public string parameter;
        public EmitterGameEvent TriggerEvent;
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

        protected override void HandleGameEvent(EmitterGameEvent gameEvent)
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
                    RuntimeUtils.DebugLogError(string.Format(("[FMOD] StudioGlobalParameterTrigger failed to set parameter {0} : result = {1}"), parameter, result));
                }
            }
        }
    }
}