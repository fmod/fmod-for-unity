using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
namespace FMOD
{
    public partial class VERSION
    {
        public const string dll = "fmodstudioL";
    }
}

namespace FMOD.Studio
{
    public partial class STUDIO_VERSION
    {
        public const string dll = "fmodstudioL";
    }
}
#endif

namespace FMODUnity
{
    public class PlatformPlayInEditor : Platform
    {
        public PlatformPlayInEditor()
        {
            Identifier = "playInEditor";
        }

        public override string DisplayName { get { return "Editor"; } }
        public override void DeclareUnityMappings(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.OSXEditor, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.WindowsEditor, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.LinuxEditor, this);
        }
#if UNITY_EDITOR
        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.PlayInEditor; } }

        protected override IEnumerable<string> GetRelativeBinaryPaths(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            yield break;
        }
#endif

        public override bool IsIntrinsic { get { return true; } }

        public override string GetBankFolder()
        {
            // Use original asset location because streaming asset folder will contain platform specific banks
            Settings globalSettings = Settings.Instance;

            string bankFolder = globalSettings.SourceBankPath;
            if (globalSettings.HasPlatforms)
            {
                bankFolder = RuntimeUtils.GetCommonPlatformPath(Path.Combine(bankFolder, BuildDirectory));
            } 

            return bankFolder;
        }

#if UNITY_EDITOR
        public override string GetPluginPath(string pluginName)
        {
#if UNITY_EDITOR_WIN && UNITY_EDITOR_64
            return string.Format("{0}/win/X86_64/{1}.dll", GetEditorPluginBasePath(), pluginName);
#elif UNITY_EDITOR_WIN
            return string.Format("{0}/win/X86/{1}.dll", GetEditorPluginBasePath(), pluginName);
#elif UNITY_EDITOR_OSX
            return string.Format("{0}/mac/{1}.bundle", GetEditorPluginBasePath(), pluginName);
#elif UNITY_EDITOR_LINUX && UNITY_EDITOR_64
            return string.Format("{0}/linux/x86_64/lib{1}.so", GetEditorPluginBasePath(), pluginName);
#elif UNITY_EDITOR_LINUX
            return string.Format("{0}/linux/x86/lib{1}.so", GetEditorPluginBasePath(), pluginName);
#endif
        }
#endif

        public override void LoadStaticPlugins(FMOD.System coreSystem, Action<FMOD.RESULT, string> reportResult)
        {
            // Ignore static plugins when playing in the editor
        }

        public override void InitializeProperties()
        {
            base.InitializeProperties();

            PropertyAccessors.LiveUpdate.Set(this, TriStateBool.Enabled);
            PropertyAccessors.Overlay.Set(this, TriStateBool.Enabled);
            PropertyAccessors.SampleRate.Set(this, 48000);
            PropertyAccessors.RealChannelCount.Set(this, 256);
            PropertyAccessors.VirtualChannelCount.Set(this, 1024);
        }
#if UNITY_EDITOR
        public override OutputType[] ValidOutputTypes { get { return null; } }
#endif
    }
}
