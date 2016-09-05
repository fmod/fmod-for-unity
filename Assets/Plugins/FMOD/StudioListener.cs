using System;
using UnityEngine;
using System.Collections;

namespace FMODUnity
{
    public enum FMODStudioListenerMode
    {
        RigidBody3D = 0,
        RigidBody2D = 1
    }

    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
        [SerializeField]
        public FMODStudioListenerMode mode;

        Rigidbody rigidBody;
        Rigidbody2D rigidBody2d;

        void OnEnable()
        {
            RuntimeUtils.EnforceLibraryOrder();
            CacheRigidBody();
            RuntimeManager.HasListener = true;
            SetListenerLocation();
        }

        void OnDisable()
        {
            RuntimeManager.HasListener = false;
        }

        void Update()
        {
            SetListenerLocation();
        }

        private void SetListenerLocation()
        {
            switch (mode)
            {
                case FMODStudioListenerMode.RigidBody2D:
                    RuntimeManager.SetListenerLocation(gameObject, rigidBody2d);
                    break;

                case FMODStudioListenerMode.RigidBody3D:
                    RuntimeManager.SetListenerLocation(gameObject, rigidBody);
                    break;

                default:
                    throw new System.NotImplementedException();
            }
        }

        private void CacheRigidBody()
        {
            switch (mode)
            {
                case FMODStudioListenerMode.RigidBody2D:
                    rigidBody2d = gameObject.GetComponent<Rigidbody2D>();
                    break;

                case FMODStudioListenerMode.RigidBody3D:
                    rigidBody = gameObject.GetComponent<Rigidbody>();
                    break;

                default:
                    throw new System.NotImplementedException();
            }
        }
    }
}
