struct FMOD_SYSTEM;
struct FMOD_DSP_DESCRIPTION;

extern "C" uint32_t FMOD5_System_RegisterDSP(FMOD_SYSTEM *system, const FMOD_DSP_DESCRIPTION *description, uint32_t *handle);

extern FMOD_DSP_DESCRIPTION* FMOD_Google_GVRListener_GetDSPDescription();
extern FMOD_DSP_DESCRIPTION* FMOD_Google_GVRSoundfield_GetDSPDescription();
extern FMOD_DSP_DESCRIPTION* FMOD_Google_GVRSource_GetDSPDescription();

extern FMOD_DSP_DESCRIPTION* FMOD_ResonanceAudioListener_GetDSPDescription();
extern FMOD_DSP_DESCRIPTION* FMOD_ResonanceAudioSoundfield_GetDSPDescription();
extern FMOD_DSP_DESCRIPTION* FMOD_ResonanceAudioSource_GetDSPDescription();

extern "C" uint32_t FmodUnityNativePluginInit(FMOD_SYSTEM* system)
{
    uint32_t result = 0;
    
    /*
    
    This function is invoked on iOS and tvOS after the system has been
    initialized and before any banks are loaded. It can be used to manually
    register plugins that have been statically linked into the executable.
    
    Each plugin will require a separate call to FMOD_System_RegisterDSP.
    The DSP_DESCRIPTION argument is the same as what is returned by 
    FMODGetDSPDescription when building a dynamic plugin.
    
    */
    
    /*
    result = FMOD5_System_RegisterDSP(system, GetMyDSPDescription(), nullptr);
    if (result != 0)
    {
        return result;
    }
    */

    /* Uncomment this next section to use the GoogleVR plugin on iOS */
    /*
    result = FMOD5_System_RegisterDSP(system, FMOD_Google_GVRListener_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    result = FMOD5_System_RegisterDSP(system, FMOD_Google_GVRSoundfield_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    result = FMOD5_System_RegisterDSP(system, FMOD_Google_GVRSource_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    */

    /* Uncomment this next section to use the Resonance Audio plugin on iOS */
    /*
    result = FMOD5_System_RegisterDSP(system, FMOD_ResonanceAudioListener_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    result = FMOD5_System_RegisterDSP(system, FMOD_ResonanceAudioSoundfield_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    result = FMOD5_System_RegisterDSP(system, FMOD_ResonanceAudioSource_GetDSPDescription(), nullptr);
    if (result != 0)
    {
      return result;
    }
    */

    return result;
}
