using System;
using UnityEngine;
using System.Collections;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
        Rigidbody rigidBody;
        Rigidbody2D rigidBody2D;

        public int ListenerNumber = 0;

        void OnEnable()
        {
            RuntimeUtils.EnforceLibraryOrder();
            rigidBody = gameObject.GetComponent<Rigidbody>();
            rigidBody2D = gameObject.GetComponent<Rigidbody2D>();
            RuntimeManager.HasListener[ListenerNumber] = true;
            SetListenerLocation();
        }

        void OnDisable()
        {
            RuntimeManager.HasListener[ListenerNumber] = false;
        }

        void Update()
        {
            SetListenerLocation();
        }

        void SetListenerLocation()
        {
            if (rigidBody)
            {
                RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody);
            }
            else
            {
                RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody2D);
            }
        }
    }
}
