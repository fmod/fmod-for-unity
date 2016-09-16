using System;
using UnityEngine;
using System.Collections;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
        Rigidbody rigidBody;

        public int ListenerNumber = 0;

        void OnEnable()
        {
            RuntimeUtils.EnforceLibraryOrder();
            rigidBody = gameObject.GetComponent<Rigidbody>();
            RuntimeManager.HasListener[ListenerNumber] = true;
            RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody);
        }

        void OnDisable()
        {
            RuntimeManager.HasListener[ListenerNumber] = false;
        }

        void Update()
        {
            RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody);
        }
    }
}
