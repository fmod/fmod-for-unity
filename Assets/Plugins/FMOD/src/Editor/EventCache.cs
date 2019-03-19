using System;
using System.Collections.Generic;
using UnityEngine;

namespace FMODUnity
{
    class EventCache : ScriptableObject
    {
        public static int CurrentCacheVersion = 3;

        [SerializeField]
        public List<EditorBankRef> EditorBanks;
        [SerializeField]
        public List<EditorEventRef> EditorEvents;
        [SerializeField]
        public List<EditorParamRef> EditorParameters;
        [SerializeField]
        public List<EditorBankRef> MasterBanks;
        [SerializeField]
        public List<EditorBankRef> StringsBanks;
        [SerializeField]
        Int64 stringsBankWriteTime;
        [SerializeField]
        public int cacheVersion;

        public DateTime StringsBankWriteTime
        {
            get { return new DateTime(stringsBankWriteTime); }
            set { stringsBankWriteTime = value.Ticks; }
        }

        public EventCache()
        {
            EditorBanks = new List<EditorBankRef>();
            EditorEvents = new List<EditorEventRef>();
            EditorParameters = new List<EditorParamRef>();
            MasterBanks = new List<EditorBankRef>();
            StringsBanks = new List<EditorBankRef>();
            stringsBankWriteTime = 0;
        }
    }
}
