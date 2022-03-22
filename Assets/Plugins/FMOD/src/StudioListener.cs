using UnityEngine;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
        public int ListenerNumber = -1;

        [SerializeField]
        private GameObject attenuationObject;

#if UNITY_PHYSICS_EXIST
        private Rigidbody rigidBody;
#endif
#if UNITY_PHYSICS2D_EXIST
        private Rigidbody2D rigidBody2D;
#endif

        private void OnEnable()
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

        private void OnDisable()
        {
            RuntimeManager.RemoveListener(this);
        }

        private void Update()
        {
            if (ListenerNumber >= 0 && ListenerNumber < FMOD.CONSTANTS.MAX_LISTENERS)
            {
                SetListenerLocation();
            }
        }

        private void SetListenerLocation()
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