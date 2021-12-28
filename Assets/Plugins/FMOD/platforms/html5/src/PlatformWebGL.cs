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

        public override string DisplayName { get { return "WebGL"; } }
        public override void DeclareRuntimePlatforms(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.WebGLPlayer, this);
        }

#if UNITY_EDITOR
        public override IEnumerable<BuildTarget> GetBuildTargets()
        {
            yield return BuildTarget.WebGL;
        }

        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.WebGL; } }

        protected override BinaryAssetFolderInfo GetBinaryAssetFolder(BuildTarget buildTarget)
        {
            return new BinaryAssetFolderInfo("html5", "Plugins/WebGL");
        }

        protected override IEnumerable<FileRecord> GetBinaryFiles(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            #if UNITY_2021_2_OR_NEWER
            bool useWASM = true;
            #else
            bool useWASM = false;
            #endif

            if (allVariants || useWASM)
            {
                yield return new FileRecord(string.Format("2.0.19/libfmodstudio{0}.a", suffix));
            }

            if (allVariants || !useWASM)
            {
                yield return new FileRecord(string.Format("libfmodstudiounityplugin{0}.bc", suffix));
            }
        }

        public override bool IsFMODStaticallyLinked { get { return true; } }
#endif

        public override string GetPluginPath(string pluginName)
        {
            #if UNITY_2021_2_OR_NEWER
            return string.Format("{0}/{1}.a", GetPluginBasePath(), pluginName);
            #else
            return string.Format("{0}/{1}.bc", GetPluginBasePath(), pluginName);
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
           new OutputType() { displayName = "JavaScript webaudio output", outputType = FMOD.OUTPUTTYPE.WEBAUDIO },
        };
#endif
    }
}
