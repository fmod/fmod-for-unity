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
        public override void DeclareRuntimePlatforms(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.IPhonePlayer, this);
        }

#if UNITY_EDITOR
        public override IEnumerable<BuildTarget> GetBuildTargets()
        {
            yield return BuildTarget.iOS;
        }

        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.iOS; } }

        protected override BinaryAssetFolderInfo GetBinaryAssetFolder(BuildTarget buildTarget)
        {
            return new BinaryAssetFolderInfo("ios", "Plugins/iOS");
        }

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK)
            {
                yield return new FileRecord(string.Format("libfmodstudiounityplugin{0}.a", suffix));
            }

            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
            {
                yield return new FileRecord(string.Format("libfmodstudiounitypluginsimulator{0}.a", suffix));
            }
        }

        protected override IEnumerable<FileRecord> GetOptionalBinaryFiles(BuildTarget buildTarget, bool allVariants)
        {
            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.DeviceSDK)
            {
                yield return new FileRecord("libgvraudio.a");
                yield return new FileRecord("libresonanceaudio.a");
            }

            if (allVariants || PlayerSettings.iOS.sdkVersion == iOSSdkVersion.SimulatorSDK)
            {
                yield return new FileRecord("libresonanceaudiosimulator.a");
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
           new OutputType() { displayName = "Core Audio", outputType = FMOD.OUTPUTTYPE.COREAUDIO },
        };
#endif
    }
}
