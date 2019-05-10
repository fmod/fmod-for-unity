using UnityEngine;

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
            if (RuntimeManager.AddListener(ListenerNumber) == false)
            {
                ListenerNumber = -1;
                this.enabled = false;
            }
            else
            {
                SetListenerLocation();
            }
        }

        void OnDisable()
        {
            RuntimeManager.RemoveListener(ListenerNumber);
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