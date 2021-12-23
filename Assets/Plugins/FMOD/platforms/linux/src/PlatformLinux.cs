using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
        public const string dll = "fmodstudio" + dllSuffix;
    }
}

namespace FMOD.Studio
{
    public partial class STUDIO_VERSION
    {
        public const string dll = "fmodstudio" + dllSuffix;
    }
}
#endif

namespace FMODUnity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class PlatformLinux : Platform
    {
        static PlatformLinux()
        {
            Settings.AddPlatformTemplate<PlatformLinux>("b7716510a1f36934c87976f3a81dbf3d");
        }

        public override string DisplayName { get { return "Linux"; } }
        public override void DeclareUnityMappings(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.LinuxPlayer, this);

#if UNITY_EDITOR
            settings.DeclareBuildTarget(BuildTarget.StandaloneLinux64, this);
#if !UNITY_2019_2_OR_NEWER
            settings.DeclareBuildTarget(BuildTarget.StandaloneLinux, this);
            settings.DeclareBuildTarget(BuildTarget.StandaloneLinuxUniversal, this);
#endif
#endif
        }

#if UNITY_EDITOR
        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.Linux; } }

        protected override IEnumerable<string> GetRelativeBinaryPaths(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneLinux64:
                    yield return string.Format("linux/x86_64/libfmodstudio{0}.so", suffix);
                    break;
#if !UNITY_2019_2_OR_NEWER
                case BuildTarget.StandaloneLinux:
                    yield return string.Format("linux/x86/libfmodstudio{0}.so", suffix);
                    break;
                case BuildTarget.StandaloneLinuxUniversal:
                    yield return string.Format("linux/x86/libfmodstudio{0}.so", suffix);
                    yield return string.Format("linux/x86_64/libfmodstudio{0}.so", suffix);
                    break;
#endif
                default:
                    throw new System.NotSupportedException("Unrecognised Build Target");

            }
        }
#endif

        public override string GetPluginPath(string pluginName)
        {
#if UNITY_2019_1_OR_NEWER
            return string.Format("{0}/lib{1}.so", GetPluginBasePath(), pluginName);
#else
            if (System.IntPtr.Size == 8)
            {
                return string.Format("{0}/x86_64/lib{1}.so", GetPluginBasePath(), pluginName);
            }
            else
            {
                return string.Format("{0}/x86/lib{1}.so", GetPluginBasePath(), pluginName);
            }
#endif
        }
#if UNITY_EDITOR
        public override OutputType[] ValidOutputTypes
        {
            get
            {
                return sValidOutputTypes;
            }
        }

        private static OutputType[] sValidOutputTypes = {
           new OutputType() { displayName = "Pulse Audio", outputType = FMOD.OUTPUTTYPE.PULSEAUDIO },
           new OutputType() { displayName = "Advanced Linux Sound Architecture", outputType = FMOD.OUTPUTTYPE.ALSA },
        };
#endif
    }
}
