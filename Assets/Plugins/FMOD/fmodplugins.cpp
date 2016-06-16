struct FMOD_SYSTEM;
struct FMOD_DSP_DESCRIPTION;

extern "C" uint32_t FMOD5_System_RegisterDSP(FMOD_SYSTEM *system, const FMOD_DSP_DESCRIPTION *description, uint32_t *handle);

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
    
    return result;
}
