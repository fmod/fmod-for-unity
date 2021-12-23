using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if !UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
#if UNITY_STANDALONE_WIN
        public const string dll = "fmodstudio" + dllSuffix;
#elif UNITY_WSA
        public const string dll = "fmod" + dllSuffix;
#endif
    }
}

namespace FMOD.Studio
{
    public partial class STUDIO_VERSION
    {
#if UNITY_STANDALONE_WIN || UNITY_WSA
        public const string dll = "fmodstudio" + dllSuffix;
#endif
    }
}
#endif

namespace FMODUnity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class PlatformWindows : Platform
    {
        static PlatformWindows()
        {
            Settings.AddPlatformTemplate<PlatformWindows>("2c5177b11d81d824dbb064f9ac8527da");
        }

        public override string DisplayName { get { return "Windows"; } }
        public override void DeclareUnityMappings(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.WindowsPlayer, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.WSAPlayerX86, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.WSAPlayerX64, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.WSAPlayerARM, this);

#if UNITY_EDITOR
            settings.DeclareBuildTarget(BuildTarget.StandaloneWindows, this);
            settings.DeclareBuildTarget(BuildTarget.StandaloneWindows64, this);
            settings.DeclareBuildTarget(BuildTarget.WSAPlayer, this);
#endif
        }

#if UNITY_EDITOR
        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.Windows; } }
#endif

#if UNITY_WINRT_8_1 || UNITY_WSA_10_0
        public override string GetBankFolder()
        {
            return "ms-appx:///Data/StreamingAssets";
        }
#endif

#if UNITY_EDITOR
        protected override IEnumerable<string> GetRelativeBinaryPaths(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            string dllSuffix = suffix + ".dll";

            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                    yield return "win/x86/fmodstudio" + dllSuffix;
                    break;
                case BuildTarget.StandaloneWindows64:
                    yield return "win/x86_64/fmodstudio" + dllSuffix;
                    break;
                case BuildTarget.WSAPlayer:
                    foreach (string architecture in new[] { "arm", "x64", "x86" })
                    {
                        yield return string.Format("uwp/{0}/fmod{1}", architecture, dllSuffix);
                        yield return string.Format("uwp/{0}/fmodstudio{1}", architecture, dllSuffix);
                    }
                    break;
                default:
                    throw new System.NotSupportedException("Unrecognised Build Target");
            }
        }

        public override bool SupportsAdditionalCPP(BuildTarget target)
        {
            return target != BuildTarget.WSAPlayer;
        }
#endif

        public override string GetPluginPath(string pluginName)
        {
#if UNITY_STANDALONE_WIN
    #if UNITY_2019_1_OR_NEWER
        #if UNITY_64
            return string.Format("{0}/X86_64/{1}.dll", GetPluginBasePath(), pluginName);
        #else
            return string.Format("{0}/X86/{1}.dll", GetPluginBasePath(), pluginName);
        #endif
    #else
            return string.Format("{0}/{1}.dll", GetPluginBasePath(), pluginName);
    #endif
#else // UNITY_WSA
            return string.Format("{0}.dll", pluginName);
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
           new OutputType() { displayName = "Windows Audio Session API", outputType = FMOD.OUTPUTTYPE.WASAPI },
           new OutputType() { displayName = "Windows Sonic", outputType = FMOD.OUTPUTTYPE.WINSONIC },
        };

        public override int CoreCount { get { return MaximumCoreCount; } }
#endif
    }
}
