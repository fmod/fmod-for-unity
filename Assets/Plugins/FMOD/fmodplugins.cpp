#include "fmod.h"


FMOD_RESULT F_API FMOD5_System_GetVersion(FMOD_SYSTEM *system, unsigned int *version);
FMOD_RESULT F_API FMOD5_System_RegisterDSP(FMOD_SYSTEM *system, const FMOD_DSP_DESCRIPTION *description, unsigned int *handle);

extern "C" FMOD_RESULT FmodUnityNativePluginInit(FMOD_SYSTEM* system)
{
    FMOD_RESULT result = FMOD_OK;
    
    
    unsigned int version=0;
    result = FMOD5_System_GetVersion(system, &version);
    
    /*
    
    This function is invoked on iOS and tvOS after the system has been
    initialized and before any banks are loaded. It can be used to manually
    register plugins that have been statically linked into the executable.
    
    Each plugin will require a separate call to FMOD_System_RegisterDSP.
    The DSP_DESCRIPTION argument is the same as what is returned by 
    FMODGetDSPDescription when building a dynamic plugin.
    
    */
    
    /*    
    result = FMOD_System_RegisterDSP(system, GetMyDSPDescription(), nullptr);
    if (result != FMOD_OK)
    {
        return result;
    }    
    */
    
    return result;
}