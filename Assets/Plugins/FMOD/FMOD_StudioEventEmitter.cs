/* This class is now legacy. Keep the definition here for the migration script to work */
using UnityEngine;

[AddComponentMenu("")]
public class FMOD_StudioEventEmitter : MonoBehaviour
{
    [Header("This component is obsolete. Use FMODUnity.StudioEventEmitter instead")]
    public FMODAsset asset;
	public string path = "";
	public bool startEventOnAwake = true;

}
