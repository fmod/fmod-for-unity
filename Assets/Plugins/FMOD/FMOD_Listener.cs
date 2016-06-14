/* This class is now legacy. Keep the definition here for the migration script to work */
using UnityEngine;

[AddComponentMenu("")]
public class FMOD_Listener : MonoBehaviour 
{
    [Header("This component is obsolete. Use FMODUnity.StudioListener instead")]
	public string[] pluginPaths = {};	
}
