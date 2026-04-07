using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
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
    public class PlatformWebGL : Platform
    {
        static PlatformWebGL()
        {
            Settings.AddPlatformTemplate<PlatformWebGL>("46fbfdf3fc43db0458918377fd40293e");
        }

        internal override string DisplayName { get { return "WebGL"; } }
        internal override void DeclareRuntimePlatforms(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.WebGLPlayer, this);
        }

#if UNITY_EDITOR
        internal override IEnumerable<BuildTarget> GetBuildTargets()
        {
            yield return BuildTarget.WebGL;
#if UNITY_WEIXINMINIGAME
            yield return BuildTarget.WeixinMiniGame;
#endif
        }

        internal override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.WebGL; } }

        protected override BinaryAssetFolderInfo GetBinaryAssetFolder(BuildTarget buildTarget)
        {
            return new BinaryAssetFolderInfo("html5", "Plugins/WebGL");
        }

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            bool emVer_2_0_19 = false;
            bool emVer_3_1_8 = false;
            bool emVer_3_1_39 = false;

#if UNITY_6000_0_OR_NEWER
            emVer_3_1_39 = true;
#elif UNITY_2022_3_OR_NEWER
            emVer_3_1_8 = true;
#else
            emVer_2_0_19 = true;
#endif

            if (allVariants || emVer_3_1_39)
            {
                yield return new FileRecord(string.Format("3.1.39/libfmodstudio{0}.a", suffix));
            }

            if (allVariants || emVer_3_1_8)
            {
                yield return new FileRecord(string.Format("3.1.8/libfmodstudio{0}.a", suffix));
            }

            if (allVariants || emVer_2_0_19)
            {
                yield return new FileRecord(string.Format("2.0.19/libfmodstudio{0}.a", suffix));
            }
        }

        internal override bool IsFMODStaticallyLinked { get { return true; } }
#endif

        internal override string GetPluginPath(string pluginName)
        {
            return string.Format("{0}/{1}.a", GetPluginBasePath(), pluginName);
        }
#if UNITY_EDITOR
        internal override OutputType[] ValidOutputTypes
        {
            get
            {
                return sValidOutputTypes;
            }
        }

        private static OutputType[] sValidOutputTypes = {
           new OutputType() { displayName = "JavaScript webaudio output", outputType = FMOD.OUTPUTTYPE.WEBAUDIO },
        };
#endif
    }
}
