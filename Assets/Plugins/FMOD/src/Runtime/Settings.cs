using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FMODUnity
{
    [Serializable]
    public enum FMODPlatform
    {
        None,
        PlayInEditor,
        Default,
        Desktop,
        Mobile,
        MobileHigh,
        MobileLow,
        Console,
        Windows,
        Mac,
        Linux,
        iOS,
        Android,
        Deprecated_1,
        XboxOne,
        PS4,
        Deprecated_2,
        Deprecated_3,
        AppleTV,
        UWP,
        Switch,
        WebGL,
        Count,
    }

    [Serializable]
    public enum ImportType
    {
        StreamingAssets,
        AssetBundle,
    }

    [Serializable]
    public enum BankLoadType
    {
        All,
        Specified,
        None
    }


    public class PlatformSettingBase
    {
        public FMODPlatform Platform;
    }

    public class PlatformSetting<T> : PlatformSettingBase
    {
        public T Value;
    }

    [Serializable]
    public class PlatformIntSetting : PlatformSetting<int>
    {
    }

    [Serializable]
    public class PlatformStringSetting : PlatformSetting<string>
    {
    }

    public enum TriStateBool
    {
        Disabled,
        Enabled,
        Development,
    }

    [Serializable]
    public class PlatformBoolSetting : PlatformSetting<TriStateBool>
    {
    }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    public class Settings : ScriptableObject
    {
        [SerializeField]
        bool SwitchSettingsMigration = false;

        const string SettingsAssetName = "FMODStudioSettings";

        private static Settings instance = null;

        public static Settings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load(SettingsAssetName) as Settings;
                    if (instance == null)
                    {
                        UnityEngine.Debug.Log("[FMOD] Cannot find integration settings, creating default settings");
                        instance = CreateInstance<Settings>();
                        instance.name = "FMOD Studio Integration Settings";

                        #if UNITY_EDITOR
                        if (!Directory.Exists("Assets/Plugins/FMOD/Resources"))
                        {
                            AssetDatabase.CreateFolder("Assets/Plugins/FMOD", "Resources");
                        }
                        AssetDatabase.CreateAsset(instance, "Assets/Plugins/FMOD/Resources/" + SettingsAssetName + ".asset");
                        #endif
                    }
                }
                return instance;
            }
        }

        #if UNITY_EDITOR
        [MenuItem("FMOD/Edit Settings", priority = 0)]
        public static void EditSettings()
        {
            Selection.activeObject = Instance;
            #if UNITY_2018_2_OR_NEWER
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            #else
            EditorApplication.ExecuteMenuItem("Window/Inspector");
            #endif
        }
        #endif

        [SerializeField]
        public bool HasSourceProject = true;

        [SerializeField]
        public bool HasPlatforms = true;

        [SerializeField]
        private string sourceProjectPath;

        public string SourceProjectPath
        {
            get
            {
                if (string.IsNullOrEmpty(sourceProjectPath) && !string.IsNullOrEmpty(SourceProjectPathUnformatted))
                {
                    sourceProjectPath = GetPlatformSpecificPath(SourceProjectPathUnformatted);
                }
                return sourceProjectPath;
            }
            set
            {
                sourceProjectPath = GetPlatformSpecificPath(value);
            }
        }

        [SerializeField]
        public string SourceProjectPathUnformatted;

        private string sourceBankPath;
        public string SourceBankPath
        {
            get
            {
                if (String.IsNullOrEmpty(sourceBankPath) && !String.IsNullOrEmpty(SourceBankPathUnformatted))
                {
                    sourceBankPath = GetPlatformSpecificPath(SourceBankPathUnformatted);
                }
                return sourceBankPath;
            }
            set
            {
            	sourceBankPath = GetPlatformSpecificPath(value);
            }
        }

        [SerializeField]
        public string SourceBankPathUnformatted;

        [SerializeField]
        public bool AutomaticEventLoading;

        [SerializeField]
        public BankLoadType BankLoadType;

        [SerializeField]
        public bool AutomaticSampleLoading;

        [SerializeField]
        public string EncryptionKey;
        
        [SerializeField]
        public ImportType ImportType;

        [SerializeField]
        public string TargetAssetPath;

        [SerializeField]
        public FMOD.DEBUG_FLAGS LoggingLevel = FMOD.DEBUG_FLAGS.WARNING;

        [SerializeField]
        public List<PlatformIntSetting> SpeakerModeSettings;

        [SerializeField]
        public List<PlatformIntSetting> SampleRateSettings;

        [SerializeField]
        public List<PlatformBoolSetting> LiveUpdateSettings;

        [SerializeField]
        public List<PlatformBoolSetting> OverlaySettings;

        [SerializeField]
        public List<PlatformBoolSetting> LoggingSettings;

        [SerializeField]
        public List<PlatformStringSetting> BankDirectorySettings;

        [SerializeField]
        public List<PlatformIntSetting> VirtualChannelSettings;

        [SerializeField]
        public List<PlatformIntSetting> RealChannelSettings;

        [SerializeField]
        public List<string> Plugins = new List<string>();

        [SerializeField]
        public List<string> MasterBanks;

        [SerializeField]
        public List<string> Banks;

        [SerializeField]
        public List<string> BanksToLoad;

        [SerializeField]
        public ushort LiveUpdatePort = 9264;

        public static FMODPlatform GetParent(FMODPlatform platform)
        {
            switch (platform)
            {
                case FMODPlatform.Windows:
                case FMODPlatform.Linux:
                case FMODPlatform.Mac:
                case FMODPlatform.UWP:
                    return FMODPlatform.Desktop;
                case FMODPlatform.MobileHigh:
                case FMODPlatform.MobileLow:
                case FMODPlatform.iOS:
                case FMODPlatform.Android:
                case FMODPlatform.AppleTV:
                    return FMODPlatform.Mobile;
                case FMODPlatform.Switch:
                case FMODPlatform.XboxOne:
                case FMODPlatform.PS4:
                    return FMODPlatform.Console;
                case FMODPlatform.Desktop:
                case FMODPlatform.Console:
                case FMODPlatform.Mobile:
                    return FMODPlatform.Default;
                case FMODPlatform.PlayInEditor:
                case FMODPlatform.Default:
                default:
                    return FMODPlatform.None;
            }
        }

        public static bool HasSetting<T>(List<T> list, FMODPlatform platform) where T : PlatformSettingBase
        {
            return list.Exists((x) => x.Platform == platform);
        }

        public static U GetSetting<T, U>(List<T> list, FMODPlatform platform, U def) where T : PlatformSetting<U>
        {
            T t = list.Find((x) => x.Platform == platform);
            if (t == null)
            {
                FMODPlatform parent = GetParent(platform);
                if (parent != FMODPlatform.None)
                {
                    return GetSetting(list, parent, def);
                }
                else
                {
                    return def;
                }
            }
            return t.Value;
        }

        public static void SetSetting<T, U>(List<T> list, FMODPlatform platform, U value) where T : PlatformSetting<U>, new()
        {
            T setting = list.Find((x) => x.Platform == platform);
            if (setting == null)
            {
                setting = new T();
                setting.Platform = platform;
                list.Add(setting);
            }
            setting.Value = value;
        }

        public static void RemoveSetting<T>(List<T> list, FMODPlatform platform) where T : PlatformSettingBase
        {
            list.RemoveAll((x) => x.Platform == platform);
        }

        // --------   Live Update ----------------------
        public bool IsLiveUpdateEnabled(FMODPlatform platform)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            return GetSetting(LiveUpdateSettings, platform, TriStateBool.Disabled) != TriStateBool.Disabled;
            #else
            return GetSetting(LiveUpdateSettings, platform, TriStateBool.Disabled) == TriStateBool.Enabled;
            #endif
        }

        // --------   Overlay Update ----------------------
        public bool IsOverlayEnabled(FMODPlatform platform)
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            return GetSetting(OverlaySettings, platform, TriStateBool.Disabled) != TriStateBool.Disabled;
            #else
            return GetSetting(OverlaySettings, platform, TriStateBool.Disabled) == TriStateBool.Enabled;
            #endif
        }

        // --------   Real channels ----------------------
        public int GetRealChannels(FMODPlatform platform)
        {
            return GetSetting(RealChannelSettings, platform, 64);
        }

        // --------   Virtual channels ----------------------
        public int GetVirtualChannels(FMODPlatform platform)
        {
            return GetSetting(VirtualChannelSettings, platform, 128);
        }

        // --------   Speaker Mode ----------------------
        public int GetSpeakerMode(FMODPlatform platform)
        {
            #if UNITY_EDITOR
            if (platform == FMODPlatform.PlayInEditor)
            { 
                return GetSetting(SpeakerModeSettings, platform, GetSetting(SpeakerModeSettings, RuntimeUtils.GetEditorFMODPlatform(), (int)FMOD.SPEAKERMODE.STEREO));
            }
            else
            #endif
            {
                return GetSetting(SpeakerModeSettings, platform, (int)FMOD.SPEAKERMODE.STEREO);
            }
        }
        // --------   Sample Rate ----------------------
        public int GetSampleRate(FMODPlatform platform)
        {
            return GetSetting(SampleRateSettings, platform, 48000);
        }

        // --------   Bank Platform ----------------------
        public string GetBankPlatform(FMODPlatform platform)
        {
            if (!HasPlatforms)
            {
                return "";
            }
            #if UNITY_EDITOR
            if (platform == FMODPlatform.PlayInEditor)
            {
                return GetSetting(BankDirectorySettings, platform, GetSetting(BankDirectorySettings, RuntimeUtils.GetEditorFMODPlatform(), "Desktop"));
            }
            else
            #endif
            { 
                return GetSetting(BankDirectorySettings, platform, "Desktop");
            }
        }

        private Settings()
        {
            MasterBanks = new List<string>();
            Banks = new List<string>();
            RealChannelSettings = new List<PlatformIntSetting>();
            VirtualChannelSettings = new List<PlatformIntSetting>();
            LoggingSettings = new List<PlatformBoolSetting>();
            LiveUpdateSettings = new List<PlatformBoolSetting>();
            OverlaySettings = new List<PlatformBoolSetting>();
            SampleRateSettings = new List<PlatformIntSetting>();
            SpeakerModeSettings = new List<PlatformIntSetting>();
            BankDirectorySettings = new List<PlatformStringSetting>();
            
            // Default play in editor settings
            SetSetting(LoggingSettings, FMODPlatform.PlayInEditor, TriStateBool.Enabled);
            SetSetting(LiveUpdateSettings, FMODPlatform.PlayInEditor, TriStateBool.Enabled);
            SetSetting(OverlaySettings, FMODPlatform.PlayInEditor, TriStateBool.Enabled);
            SetSetting(SampleRateSettings, FMODPlatform.PlayInEditor, 48000);
            // These are not editable, set them high
            SetSetting(RealChannelSettings, FMODPlatform.PlayInEditor, 256);
            SetSetting(VirtualChannelSettings, FMODPlatform.PlayInEditor, 1024);

            // Default runtime settings
            SetSetting(LoggingSettings, FMODPlatform.Default, TriStateBool.Disabled);
            SetSetting(LiveUpdateSettings, FMODPlatform.Default, TriStateBool.Disabled);
            SetSetting(OverlaySettings, FMODPlatform.Default, TriStateBool.Disabled);

            SetSetting(RealChannelSettings, FMODPlatform.Default, 32); // Match the default in the low level
            SetSetting(VirtualChannelSettings, FMODPlatform.Default, 128);
            SetSetting(SampleRateSettings, FMODPlatform.Default, 0);
            SetSetting(SpeakerModeSettings, FMODPlatform.Default, (int) FMOD.SPEAKERMODE.STEREO);

            ImportType = ImportType.StreamingAssets;
            AutomaticEventLoading = true;
            AutomaticSampleLoading = false;
            TargetAssetPath = "";

        }

        private void OnEnable()
        {
            if (SwitchSettingsMigration == false)
            {
                SetSetting(LoggingSettings, FMODPlatform.Switch, GetSetting(LoggingSettings, FMODPlatform.Mobile, TriStateBool.Disabled));
                SetSetting(LiveUpdateSettings, FMODPlatform.Switch, GetSetting(LiveUpdateSettings, FMODPlatform.Mobile, TriStateBool.Disabled));
                SetSetting(OverlaySettings, FMODPlatform.Switch, GetSetting(OverlaySettings, FMODPlatform.Mobile, TriStateBool.Disabled));

                SetSetting(RealChannelSettings, FMODPlatform.Switch, GetSetting(RealChannelSettings, FMODPlatform.Mobile, 32)); // Match the default in the low level
                SetSetting(VirtualChannelSettings, FMODPlatform.Switch, GetSetting(VirtualChannelSettings, FMODPlatform.Mobile, 128));
                SetSetting(SampleRateSettings, FMODPlatform.Switch, GetSetting(SampleRateSettings, FMODPlatform.Mobile, 0));
                SetSetting(SpeakerModeSettings, FMODPlatform.Switch, GetSetting(SpeakerModeSettings, FMODPlatform.Mobile, (int)FMOD.SPEAKERMODE.STEREO));
                SwitchSettingsMigration = true;
            }
        }

        private string GetPlatformSpecificPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (Path.DirectorySeparatorChar == '/')
            {
                return path.Replace('\\', '/');
            }
            return path.Replace('/', '\\');
        }
    }
}