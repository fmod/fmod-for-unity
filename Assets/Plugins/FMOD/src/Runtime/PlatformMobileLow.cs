using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FMODUnity
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class PlatformMobileLow : Platform
    {
        static PlatformMobileLow()
        {
            Settings.AddPlatformTemplate<PlatformMobileLow>("c88d16e5272a4e241b0ef0ac2e53b73d");
        }

        public override string DisplayName { get { return "Low-End Mobile"; } }
        public override void DeclareUnityMappings(Settings settings)
        {
            settings.DeclareRuntimePlatform(RuntimePlatform.IPhonePlayer, this);
            settings.DeclareRuntimePlatform(RuntimePlatform.Android, this);
        }
#if UNITY_EDITOR
        public override Legacy.Platform LegacyIdentifier { get { return Legacy.Platform.MobileLow; } }

        protected override IEnumerable<string> GetRelativeBinaryPaths(BuildTarget buildTarget, bool allVariants, string suffix)
        {
            yield break;
        }

        public override bool SupportsAdditionalCPP(BuildTarget target)
        {
            if (target == BuildTarget.iOS)
            {
                return PlatformIOS.StaticSupportsAdditionalCpp();
            }
            else
            {
                return base.SupportsAdditionalCPP(target);
            }
        }
#endif

        public override float Priority { get { return DefaultPriority + 1; } }

        public override bool MatchesCurrentEnvironment
        {
            get
            {
                return Active;
            }
        }

#if UNITY_IOS
        public override void LoadPlugins(FMOD.System coreSystem, Action<FMOD.RESULT, string> reportResult)
        {
            PlatformIOS.StaticLoadPlugins(this, coreSystem, reportResult);
        }
#elif UNITY_ANDROID
        public override string GetBankFolder()
        {
            return PlatformAndroid.StaticGetBankFolder();
        }

        public override string GetPluginPath(string pluginName)
        {
            return PlatformAndroid.StaticGetPluginPath(pluginName);
        }
#endif

#if UNITY_EDITOR
        public override OutputType[] ValidOutputTypes { get { return null; } }
#endif
    }
}
