using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
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
    public class PlatformVisionOS : Platform
    {
        static PlatformVisionOS()
        {
            Settings.AddPlatformTemplate<PlatformVisionOS>("de700ef3f37a49b58a57ae3addf01ad9");
        }

        internal override string DisplayName { get { return "visionOS"; } }
        internal override void DeclareRuntimePlatforms(Settings settings)
        {
            #if UNITY_VISIONOS
            settings.DeclareRuntimePlatform(RuntimePlatform.VisionOS, this);
            #endif
        }

#if UNITY_EDITOR
        internal override IEnumerable<BuildTarget> GetBuildTargets()
        {
            #if UNITY_VISIONOS
            yield return BuildTarget.VisionOS;
            #else
            yield return BuildTarget.NoTarget;
            #endif
        }

        internal override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.None; } }

        protected override BinaryAssetFolderInfo GetBinaryAssetFolder(BuildTarget buildTarget)
        {
            return new BinaryAssetFolderInfo("visionos", FileLayout.Release_2_2);
        }

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            #if UNITY_VISIONOS
            if (allVariants || PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Device)
            #endif
            {
                yield return new FileRecord(string.Format("libfmodstudio{0}_xros.a", suffix));
            }

            #if UNITY_VISIONOS
            if (allVariants || PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Simulator)
            #endif
            {
                yield return new FileRecord(string.Format("libfmodstudio{0}_xrsimulator.a", suffix));
            }
        }

        protected override IEnumerable<FileRecord> GetOptionalBinaryFiles(BuildTarget buildTarget, bool allVariants)
        {
            #if UNITY_VISIONOS
            if (allVariants || PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Device)
            #endif
            {
                yield return new FileRecord("libresonanceaudio_xros.a");
            }

            #if UNITY_VISIONOS
            if (allVariants || PlayerSettings.VisionOS.sdkVersion == VisionOSSdkVersion.Simulator)
            #endif
            {
                yield return new FileRecord("libresonanceaudio_xrsimulator.a");
            }
        }

        internal override bool IsFMODStaticallyLinked { get { return true; } }

        internal override bool SupportsAdditionalCPP(BuildTarget target)
        {
            return PlatformIOS.StaticSupportsAdditionalCpp();
        }
#endif

#if !UNITY_EDITOR
        internal override void LoadPlugins(FMOD.System coreSystem, Action<FMOD.RESULT, string> reportResult)
        {
            PlatformIOS.StaticLoadPlugins(this, coreSystem, reportResult);
        }
#endif

#if UNITY_EDITOR
        internal override OutputType[] ValidOutputTypes
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
