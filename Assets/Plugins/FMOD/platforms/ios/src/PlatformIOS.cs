#if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
#define USE_FMOD_NATIVE_PLUGIN_INIT
#endif

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_IPHONE && !UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
        public const string dll = "__Internal";
    }
}

namespace FMOD.Studio
{
    public partial class STUDIO_VERSION
    {
        public const string dll = "__Internal";
    }
}
#endif

namespace FMODUnity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class PlatformIOS : Platform
    {
        static PlatformIOS()
        {
            Settings.AddPlatformTemplate<PlatformIOS>("0f8eb3f400726694eb47beb1a9f94ce8");
        }

        public override string DisplayName { get { return "iOS"; } }
        public override void DeclareUnityMappings(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.IPhonePlayer, this);

#if UNITY_EDITOR
            settings.DeclareBuildTarget(BuildTarget.iOS, this);
#endif
        }

#if UNITY_EDITOR
        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.iOS; } }

        protected override IEnumerable<string> GetRelativeBinaryPaths(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK)
            {
                yield return string.Format("ios/libfmodstudiounityplugin{0}.a", suffix);
            }

            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
            {
                yield return string.Format("ios/libfmodstudiounitypluginsimulator{0}.a", suffix);
            }
        }

        protected override IEnumerable<string> GetRelativeOptionalBinaryPaths(BuildTarget buildTarget, bool allVariants)
        {
            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK)
            {
                yield return "ios/libresonanceaudio.a";
            }

            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
            {
                yield return "ios/libresonanceaudiosimulator.a";
            }
        }

        public override bool IsFMODStaticallyLinked { get { return true; } }

        public override bool SupportsAdditionalCPP(BuildTarget target)
        {
            return StaticSupportsAdditionalCpp();
        }

        public static bool StaticSupportsAdditionalCpp()
        {
            return false;
        }
#endif

        public override void LoadPlugins(FMOD.System coreSystem, Action<FMOD.RESULT, string> reportResult)
        {
            StaticLoadPlugins(this, coreSystem, reportResult);
        }

        public static void StaticLoadPlugins(Platform platform, FMOD.System coreSystem,
            Action<FMOD.RESULT, string> reportResult)
        {
            platform.LoadStaticPlugins(coreSystem, reportResult);

#if USE_FMOD_NATIVE_PLUGIN_INIT
            // Legacy static plugin system
            FmodUnityNativePluginInit(coreSystem.handle);
#endif
        }

#if USE_FMOD_NATIVE_PLUGIN_INIT
        [DllImport("__Internal")]
        private static extern FMOD.RESULT FmodUnityNativePluginInit(IntPtr system);
#endif
#if UNITY_EDITOR
        public override OutputType[] ValidOutputTypes
        {
            get
            {
                return sValidOutputTypes;
            }
        }

        private static OutputType[] sValidOutputTypes = {
           new OutputType() { displayName = "Core Audio", outputType = FMOD.OUTPUTTYPE.COREAUDIO },
        };
#endif
    }
}
