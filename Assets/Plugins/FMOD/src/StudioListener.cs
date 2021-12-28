using UnityEngine;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
#if UNITY_PHYSICS_EXIST
        Rigidbody rigidBody;
#endif
#if UNITY_PHYSICS2D_EXIST
        Rigidbody2D rigidBody2D;
#endif

        public GameObject attenuationObject;

        public int ListenerNumber = -1;

        void OnEnable()
        {
            RuntimeUtils.EnforceLibraryOrder();
#if UNITY_PHYSICS_EXIST
            rigidBody = gameObject.GetComponent<Rigidbody>();
#endif
#if UNITY_PHYSICS2D_EXIST
            rigidBody2D = gameObject.GetComponent<Rigidbody2D>();
#endif
            ListenerNumber = RuntimeManager.AddListener(this);
        }

        void OnDisable()
        {
            RuntimeManager.RemoveListener(this);
        }

        void Update()
        {
            if (ListenerNumber >= 0 && ListenerNumber < FMOD.CONSTANTS.MAX_LISTENERS)
            {
                SetListenerLocation();
            }
        }

        void SetListenerLocation()
        {
#if UNITY_PHYSICS_EXIST
            if (rigidBody)
            {
                RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody, attenuationObject);
            }
            else
#endif
#if UNITY_PHYSICS2D_EXIST
            if (rigidBody2D)
            {
                RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, rigidBody2D, attenuationObject);
            }
            else
#endif
            {
                RuntimeManager.SetListenerLocation(ListenerNumber, gameObject, attenuationObject);
            }
        }
    }
}