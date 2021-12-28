using System.Collections.Generic;
using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
        public const string dll = "fmod" + dllSuffix;
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
    public class PlatformAndroid : Platform
    {
        static PlatformAndroid()
        {
            Settings.AddPlatformTemplate<PlatformAndroid>("2fea114e74ecf3c4f920e1d5cc1c4c40");
        }

        public override string DisplayName { get { return "Android"; } }
        public override void DeclareRuntimePlatforms(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.Android, this);
        }

#if UNITY_EDITOR
        public override IEnumerable<BuildTarget> GetBuildTargets()
        {
            yield return BuildTarget.Android;
        }

        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.Android; } }

        protected override BinaryAssetFolderInfo GetBinaryAssetFolder(BuildTarget buildTarget)
        {
            return new BinaryAssetFolderInfo("android", "Plugins/Android/libs");
        }

        private static readonly string[] Architectures = { "arm64-v8a", "armeabi-v7a", "x86" };

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            yield return new FileRecord("fmod.jar")
                .WithAbsoluteVersion(FileLayout.Release_1_10, "Plugins/Android/fmod.jar");

            foreach (string architecture in Architectures)
            {
                yield return new FileRecord(string.Format("{0}/libfmod{1}.so", architecture, suffix));
                yield return new FileRecord(string.Format("{0}/libfmodstudio{1}.so", architecture, suffix));
            }
        }

        protected override IEnumerable<FileRecord> GetOptionalBinaryFiles(BuildTarget buildTarget, bool allVariants)
        {
            foreach (string architecture in Architectures)
            {
                yield return new FileRecord(string.Format("{0}/libgvraudio.so", architecture));
                yield return new FileRecord(string.Format("{0}/libresonanceaudio.so", architecture));
            }
        }

        public override bool SupportsAdditionalCPP(BuildTarget target)
        {
            // Unity parses --additional-cpp arguments specified via
            // PlayerSettings.SetAdditionalIl2CppArgs() incorrectly when the Android
            // Export Project option is set.
            return false;
        }
#endif

        public override string GetBankFolder()
        {
            return StaticGetBankFolder();
        }

        public static string StaticGetBankFolder()
        {
            return Settings.Instance.AndroidUseOBB ? Application.streamingAssetsPath : "file:///android_asset";
        }

        public override string GetPluginPath(string pluginName)
        {
            return StaticGetPluginPath(pluginName);
        }

        public static string StaticGetPluginPath(string pluginName)
        {
            return string.Format("lib{0}.so", pluginName);
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
           new OutputType() { displayName = "Java Audio Track", outputType = FMOD.OUTPUTTYPE.AUDIOTRACK },
           new OutputType() { displayName = "OpenSL ES", outputType = FMOD.OUTPUTTYPE.OPENSL },
           new OutputType() { displayName = "AAudio", outputType = FMOD.OUTPUTTYPE.AAUDIO },
        };

        public override int CoreCount { get { return MaximumCoreCount; } }
#endif
    }
}
