using System;
using UnityEngine;

namespace FMODUnity
{
    [Obsolete("Use the EventReference struct instead")]
    public class AudioEventRefAttribute : PropertyAttribute
    {
        public string MigrateTo = null;
    }
}
