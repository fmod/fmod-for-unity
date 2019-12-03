using UnityEngine;

namespace FMODUnity
{
    [AddComponentMenu("FMOD Studio/FMOD Studio Listener")]
    public class StudioListener : MonoBehaviour
    {
        Rigidbody rigidBody;
        Rigidbody2D rigidBody2D;

        public int ListenerNumber = -1;

        void OnEnable()
        {
            RuntimeUtils.EnforceLibraryOrder();
            rigidBody = gameObject.GetComponent<Rigidbody>();
            rigidBody2D = gameObject.GetComponent<Rigidbody2D>();
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