using UnityEngine;
using System.Collections.Generic;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Bank Loader")]
    public class StudioBankLoader : MonoBehaviour
    {
        public LoaderGameEvent LoadEvent;
        public LoaderGameEvent UnloadEvent;
        [BankRef]
        public List<string> Banks;
        public string CollisionTag;
        public bool PreloadSamples;
        private bool isQuitting;
        
        void HandleGameEvent(LoaderGameEvent gameEvent)
        {
            if (LoadEvent == gameEvent)
            {
                Load();
            }
            if (UnloadEvent == gameEvent)
            {
                Unload();
            }
        }

        void Start()
        {
            RuntimeUtils.EnforceLibraryOrder();
            HandleGameEvent(LoaderGameEvent.ObjectStart);
        }

        void OnApplicationQuit()
        {
            isQuitting = true;
        }

        void OnDestroy()
        {
            if (!isQuitting)
            {
                HandleGameEvent(LoaderGameEvent.ObjectDestroy);
            }
        }

#if UNITY_PHYSICS_EXIST
        void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(LoaderGameEvent.TriggerEnter);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(LoaderGameEvent.TriggerExit);
            }
        }
#endif

#if UNITY_PHYSICS2D_EXIST
        void OnTriggerEnter2D(Collider2D other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(LoaderGameEvent.TriggerEnter2D);
            }
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (string.IsNullOrEmpty(CollisionTag) || other.CompareTag(CollisionTag))
            {
                HandleGameEvent(LoaderGameEvent.TriggerExit2D);
            }
        }
#endif

        void OnEnable()
        {
            HandleGameEvent(LoaderGameEvent.ObjectEnable);
        }

        void OnDisable()
        {
            HandleGameEvent(LoaderGameEvent.ObjectDisable);
        }

        public void Load()
        {
            foreach (var bankRef in Banks)
            {
                try
                {
                    RuntimeManager.LoadBank(bankRef, PreloadSamples);
                }
                catch (BankLoadException e)
                {
                    RuntimeUtils.DebugLogException(e);
                }
            }
            RuntimeManager.WaitForAllSampleLoading();
        }

        public void Unload()
        {
            foreach (var bankRef in Banks)
            {
                RuntimeManager.UnloadBank(bankRef);
            }
        }
    }
}