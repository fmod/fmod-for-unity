using System;
using UnityEngine;

namespace FMODUnity
{
    [Serializable]
    public class ParamRef
    {
        public string Name;
        public float Value;
        public FMOD.Studio.PARAMETER_ID ID;
    }
}
