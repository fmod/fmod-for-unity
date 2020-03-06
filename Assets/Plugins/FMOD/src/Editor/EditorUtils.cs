using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Net.Sockets;

namespace FMODUnity
{
    public enum PreviewState
    {
        Stopped,
        Playing,
        Paused,
    }

    [InitializeOnLoad]
    class EditorUtils : MonoBehaviour
    {
        public static void CheckResult(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogError(string.Format("FMOD Studio: Encounterd Error: {0} {1}", result, FMOD.Error.String(result)));
            }
        }

        public const string BuildFolder = "Build";

        public static string GetBankDirectory()
        {
            if (Settings.Instance.HasSourceProject && !String.IsNullOrEmpty(Settings.Instance.SourceProjectPath))
            {
                string projectPath = Settings.Instance.SourceProjectPath;
                string projectFolder = Path.GetDirectoryName(projectPath);
                return Path.Combine(projectFolder, BuildFolder);
            }
            else if (!String.IsNullOrEmpty(Settings.Instance.SourceBankPath))
            {
                return Path.GetFullPath(Settings.Instance.SourceBankPath);
            }
            return null;
        }

        public static void ValidateSource(out bool valid, out string reason)
        {
            valid = true;
            reason = "";
            var settings = Settings.Instance;
            if (settings.HasSourceProject)
            {
                if (string.IsNullOrEmpty(settings.SourceProjectPath))
                {
                    valid = false;
                    reason = "The FMOD Studio project path must be set to an .fspro file.";
                    return;
                }
                if (!File.Exists(settings.SourceProjectPath))
                {
                    valid = false;
                    reason = string.Format("The FMOD Studio project path '{0}' does not exist.", settings.SourceProjectPath);
                    return;
                }

                string projectPath = settings.SourceProjectPath;
                string projectFolder = Path.GetDirectoryName(projectPath);
                string buildFolder = Path.Combine(projectFolder, BuildFolder);
                if (!Directory.Exists(buildFolder) ||
                    Directory.GetDirectories(buildFolder).Length == 0 ||
                    Directory.GetFiles(Directory.GetDirectories(buildFolder)[0], "*.bank", SearchOption.AllDirectories).Length == 0
                    )
                {
                    valid = false;
                    reason = string.Format("The FMOD Studio project '{0}' does not contain any built banks. Please build your project in FMOD Studio.", settings.SourceProjectPath);
                    return;
                }
            }
            else
            {
                if (String.IsNullOrEmpty(settings.SourceBankPath))
                {
                    valid = false;
                    reason = "The build path has not been set.";
                    return;
                }
                if (!Directory.Exists(settings.SourceBankPath))
                {
                    valid = false;
                    reason = string.Format("The build path '{0}' does not exist.", settings.SourceBankPath);
                    return;
                }

                if (settings.HasPlatforms)
                {
                    if (Directory.GetDirectories(settings.SourceBankPath).Length == 0)
                    {
                        valid = false;
                        reason = string.Format("Build path '{0}' does not contain any platform sub-directories. Please check that the build path is correct.", settings.SourceBankPath);
                        return;
                    }
                }
                else
                {
                    if (Directory.GetFiles(settings.SourceBankPath, "*.strings.bank").Length == 0)
                    {
                        valid = false;
                        reason = string.Format("Build path '{0}' does not contain any built banks.", settings.SourceBankPath);
                        return;
                    }
                }
            }
        }

        public static string[] GetBankPlatforms()
        {
            string buildFolder = Settings.Instance.SourceBankPath;
            try
            {
                if (Directory.GetFiles(buildFolder, "*.bank").Length == 0)
                {
                    string[] buildDirectories = Directory.GetDirectories(buildFolder);
                    string[] buildNames = new string[buildDirectories.Length];
                    for (int i = 0; i < buildDirectories.Length; i++)
                    {
                        buildNames[i] = Path.GetFileNameWithoutExtension(buildDirectories[i]);
                    }
                    return buildNames;
                }
            }
            catch
            {
            }
            return new string[0];
        }

        static string VerionNumberToString(uint version)
        {
            uint major = (version & 0x00FF0000) >> 16;
            uint minor = (version & 0x0000FF00) >> 8;
            uint patch = (version & 0x000000FF);

            return major.ToString("X1") + "." + minor.ToString("X2") + "." + patch.ToString("X2");
        }

        static EditorUtils()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += HandleOnPlayModeChanged;
            EditorApplication.pauseStateChanged += HandleOnPausedModeChanged;
        }

        static void HandleBeforeAssemblyReload()
        {
            DestroySystem();
        }

        static void HandleOnPausedModeChanged(PauseState state)
        {
            if (RuntimeManager.IsInitialized && RuntimeManager.HasBanksLoaded)
            {
                RuntimeManager.GetBus("bus:/").setPaused(EditorApplication.isPaused);
                RuntimeManager.StudioSystem.update();
            }
        }

        static void HandleOnPlayModeChanged(PlayModeStateChange state)
        {
            // Entering Play Mode will cause scripts to reload, losing all state
            // This is the last chance to clean up FMOD and avoid a leak.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DestroySystem();
            }
        }

        static void Update()
        {
            // Update the editor system
            if (system.isValid())
            {
                CheckResult(system.update());
            }

            if (previewEventInstance.isValid())
            {
                FMOD.Studio.PLAYBACK_STATE state;
                previewEventInstance.getPlaybackState(out state);
                if (previewState == PreviewState.Playing && state == FMOD.Studio.PLAYBACK_STATE.STOPPED)
                {
                    PreviewStop();
                }
            }
        }

        static FMOD.Studio.System system;

        static void DestroySystem()
        {
            if (system.isValid())
            {
                UnityEngine.Debug.Log("FMOD Studio: Destroying editor system instance");
                system.release();
                system.clearHandle();
            }
        }

        static void CreateSystem()
        {
            UnityEngine.Debug.Log("FMOD Studio: Creating editor system instance");
            RuntimeUtils.EnforceLibraryOrder();

            FMOD.RESULT result = FMOD.Debug.Initialize(FMOD.DEBUG_FLAGS.LOG, FMOD.DEBUG_MODE.FILE, null, "fmod_editor.log");
            if (result != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogWarning("FMOD Studio: Cannot open fmod_editor.log. Logging will be disabled for importing and previewing");
            }

            CheckResult(FMOD.Studio.System.create(out system));

            FMOD.System lowlevel;
            CheckResult(system.getCoreSystem(out lowlevel));

            // Use play-in-editor speaker mode for event browser preview and metering
            lowlevel.setSoftwareFormat(0, (FMOD.SPEAKERMODE)Settings.Instance.GetSpeakerMode(FMODPlatform.Default),0 );

            CheckResult(system.initialize(256, FMOD.Studio.INITFLAGS.ALLOW_MISSING_PLUGINS | FMOD.Studio.INITFLAGS.SYNCHRONOUS_UPDATE, FMOD.INITFLAGS.NORMAL, IntPtr.Zero));

            FMOD.ChannelGroup master;
            CheckResult(lowlevel.getMasterChannelGroup(out master));
            FMOD.DSP masterHead;
            CheckResult(master.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out masterHead));
            CheckResult(masterHead.setMeteringEnabled(false, true));
        }

        public static void UpdateParamsOnEmitter(SerializedObject serializedObject, string path)
        {
            if (string.IsNullOrEmpty(path) || EventManager.EventFromPath(path) == null)
            {
                return;
            }

            var eventRef = EventManager.EventFromPath(path);
            serializedObject.ApplyModifiedProperties();
            if (serializedObject.isEditingMultipleObjects)
            {
                foreach (var obj in serializedObject.targetObjects)
                {
                    UpdateParamsOnEmitter(obj, eventRef);
                }
            }
            else
            {
                UpdateParamsOnEmitter(serializedObject.targetObject, eventRef);
            }
            serializedObject.Update();
        }

        private static void UpdateParamsOnEmitter(UnityEngine.Object obj, EditorEventRef eventRef)
        {
            var emitter = obj as StudioEventEmitter;
            if (emitter == null)
            {
                // Custom game object
                return;
            }

            for (int i = 0; i < emitter.Params.Length; i++)
            {
                if (!eventRef.Parameters.Exists((x) => x.Name == emitter.Params[i].Name))
                {
                    int end = emitter.Params.Length - 1;
                    emitter.Params[i] = emitter.Params[end];
                    Array.Resize<ParamRef>(ref emitter.Params, end);
                    i--;
                }
            }
        }

        public static FMOD.Studio.System System
        {
            get
            {
                if (!system.isValid())
                {
                    CreateSystem();
                }
                return system;
            }
        }

        [MenuItem("FMOD/Help/Integration Manual", priority = 3)]
        static void OnlineManual()
        {
            Application.OpenURL("https://fmod.com/resources/documentation-unity");
        }

        [MenuItem("FMOD/Help/API Documentation", priority = 4)]
        static void OnlineAPIDocs()
        {
            Application.OpenURL("https://fmod.com/resources/documentation-api");
        }

        [MenuItem("FMOD/Help/Support Forum", priority = 5)]
        static void OnlineQA()
        {
            Application.OpenURL("https://qa.fmod.com/");
        }

        [MenuItem("FMOD/Help/Revision History", priority = 6)]
        static void OnlineRevisions()
        {
            Application.OpenURL("https://fmod.com/resources/documentation-api?version=2.0&page=welcome-revision-history.html");
        }

        [MenuItem("FMOD/About Integration", priority = 7)]
        public static void About()
        {
            FMOD.System lowlevel;
            CheckResult(System.getCoreSystem(out lowlevel));

            uint version;
            CheckResult(lowlevel.getVersion(out version));

            EditorUtility.DisplayDialog("FMOD Studio Unity Integration", "Version: " + VerionNumberToString(version) + "\n\nCopyright \u00A9 Firelight Technologies Pty, Ltd. 2014-2020 \n\nSee LICENSE.TXT for additional license information.", "OK");
        }

        [MenuItem("FMOD/Consolidate Plugin Files")]
        public static void FolderMerge()
        {
            string root = "Assets/Plugins/FMOD";
            string lib = root + "/lib";
            string src = root + "/src";
            string runtime = src + "/Runtime";
            string editor = src + "/Editor";
            string addons = root + "/addons";

            bool merge = EditorUtility.DisplayDialog("FMOD Plugin Consolidator", "This will consolidate most of the FMOD files into a single directory (Assets/Plugins/FMOD), only if the files have not been moved from their original location.\n\nThis should only need to be done if upgrading from before 2.0.", "OK", "Cancel");
            if (merge)
            {
                if (!Directory.Exists(addons))
                    AssetDatabase.CreateFolder(root, "addons");
                if (!Directory.Exists(src))
                    AssetDatabase.CreateFolder(root, "src");
                if (!Directory.Exists(runtime))
                    AssetDatabase.CreateFolder(src, "Runtime");
                if (!Directory.Exists(lib))
                    AssetDatabase.CreateFolder(root, "lib");
                if (!Directory.Exists(lib + "/mac"))
                    AssetDatabase.CreateFolder(lib, "mac");
                if (!Directory.Exists(lib + "/win"))
                    AssetDatabase.CreateFolder(lib, "win");
                if (!Directory.Exists(lib + "/linux"))
                    AssetDatabase.CreateFolder(lib, "linux");
                if (!Directory.Exists(lib + "/linux/x86"))
                    AssetDatabase.CreateFolder(lib + "/linux", "x86");
                if (!Directory.Exists(lib + "/linux/x86_64"))
                    AssetDatabase.CreateFolder(lib + "/linux", "x86_64");
                if (!Directory.Exists(lib + "/android"))
                    AssetDatabase.CreateFolder(lib, "android");

                // Scripts
                var files = Directory.GetFiles(root, "*.cs", SearchOption.TopDirectoryOnly);
                foreach (var filePath in files)
                {
                    MoveAsset(filePath, runtime + "/" + Path.GetFileName(filePath));
                }
                MoveAsset(root + "/fmodplugins.cpp", runtime + "/fmodplugins.cpp");
                MoveAsset(root + "/Timeline", runtime + "/Timeline");
                MoveAsset("Assets/Plugins/FMOD/Wrapper", runtime + "/wrapper");
                MoveAsset("Assets/Plugins/Editor/FMOD", editor);
                MoveAsset("Assets/Plugins/Editor/FMOD/Timeline", editor + "/Timeline");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/FMOD/Runtime") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/FMOD/Runtime" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/FMOD/Runtime");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Editor/FMOD") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Editor/FMOD" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/Editor/FMOD");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Editor") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Editor" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/Editor");
                // GoogleVR
                if (AssetDatabase.IsValidFolder("Assets/GoogleVR"))
                    MoveAsset("Assets/GoogleVR", addons + "/GoogleVR");
                // ResonanceAudio
                MoveAsset("Assets/ResonanceAudio", addons + "/ResonanceAudio");
                // GVR Audio
                if (AssetDatabase.IsValidFolder("Assets/Plugins/gvraudio.bundle"))
                    MoveAsset("Assets/Plugins/gvraudio.bundle", lib + "/mac/gvraudio.bundle");
                // Cache files
                MoveAsset("Assets/Resources/FMODStudioSettings.asset", root + "/Resources/FMODStudioSettings.asset");
                if (AssetDatabase.IsValidFolder("Assets/Resources") && AssetDatabase.FindAssets("", new string[] { "Assets/Resources" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Resources");
                MoveAsset("Assets/FMODStudioCache.asset", root + "/Resources/FMODStudioCache.asset");
                if (AssetDatabase.FindAssets("Assets/FMODStudioCache.asset").Length != 0)
                    AssetDatabase.MoveAssetToTrash("Assets/FMODStudioCache.asset");
                // Android libs
                string[] archs = new string[] { "armeabi-v7a", "x86", "arm64-v8a" };
                foreach (string arch in archs)
                {
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libfmod.so", lib + "/android/" + arch + "/libfmod.so");
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libfmodL.so", lib + "/android/" + arch + "/libfmodL.so");
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libfmodstudio.so", lib + "/android/" + arch + "/libfmodstudio.so");
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libfmodstudioL.so", lib + "/android/" + arch + "/libfmodstudioL.so");
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libresonanceaudio.so", lib + "/android/" + arch + "/libresonanceaudio.so");
                    MoveAsset("Assets/Plugins/Android/libs/" + arch + "/libgvraudio.so", lib + "/android/" + arch + "/libgvraudio.so");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/Android/libs/" + arch) && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Android/libs/" + arch }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/Android/libs/" + arch);
                }
                MoveAsset("Assets/Plugins/Android/fmod.jar", lib + "/android/fmod.jar");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Android/libs") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Android/libs" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/Android/libs");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Android") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Android" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/Android");
                AssetDatabase.
                // Mac libs
                MoveAsset("Assets/Plugins/fmodstudio.bundle", lib + "/mac/fmodstudio.bundle");
                MoveAsset("Assets/Plugins/fmodstudioL.bundle", lib + "/mac/fmodstudioL.bundle");
                MoveAsset("Assets/Plugins/resonanceaudio.bundle", lib + "/mac/resonanceaudio.bundle");
                // iOS libs
                MoveAsset("Assets/Plugins/iOS/libfmodstudiounityplugin.a", lib + "/ios/libfmodstudiounityplugin.a");
                MoveAsset("Assets/Plugins/iOS/libfmodstudiounitypluginL.a", lib + "/ios/libfmodstudiounitypluginL.a");
                MoveAsset("Assets/Plugins/iOS/libgvraudio.a", lib + "/ios/libgvraudio.a");
                MoveAsset("Assets/Plugins/iOS/libresonanceaudio.a", lib + "/ios/libresonanceaudio.a");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/iOS") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/iOS" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/iOS");
                // tvOS libs
                MoveAsset("Assets/Plugins/tvOS/libfmodstudiounityplugin.a", lib + "/tvos/libfmodstudiounityplugin.a");
                MoveAsset("Assets/Plugins/tvOS/libfmodstudiounitypluginL.a", lib + "/tvos/libfmodstudiounitypluginL.a");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/tvOS") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/tvOS" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/tvOS");
                // UWP libs
                archs = new string[] { "arm", "x64", "x86" };
                foreach (string arch in archs)
                {
                    MoveAsset("Assets/Plugins/UWP/" + arch + "/fmod.dll", lib + "/uwp/" + arch + "/fmod.dll");
                    MoveAsset("Assets/Plugins/UWP/" + arch + "/fmodL.dll", lib + "/uwp/" + arch + "/fmodL.dll");
                    MoveAsset("Assets/Plugins/UWP/" + arch + "/fmodstudio.dll", lib + "/uwp/" + arch + "/fmodstudio.dll");
                    MoveAsset("Assets/Plugins/UWP/" + arch + "/fmodstudioL.dll", lib + "/uwp/" + arch + "/fmodstudioL.dll");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/UWP/" + arch) && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/UWP/" + arch }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/UWP/" + arch);
                }
                if (AssetDatabase.IsValidFolder("Assets/Plugins/UWP") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/UWP" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/UWP");
                // HTML5 libs
                MoveAsset("Assets/Plugins/WebGL/libfmodstudiounityplugin.bc", lib + "/html5/libfmodstudiounityplugin.bc");
                MoveAsset("Assets/Plugins/WebGL/libfmodstudiounitypluginL.bc", lib + "/html5/libfmodstudiounitypluginL.bc");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/WebGL") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/WebGL" }).Length == 0)
                    AssetDatabase.MoveAssetToTrash("Assets/Plugins/WebGL");
                // PS4 libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/PS4"))
                {
                    MoveAsset("Assets/Plugins/PS4/libfmod.prx", lib + "/ps4/libfmod.prx");
                    MoveAsset("Assets/Plugins/PS4/libfmodL.prx", lib + "/ps4/libfmodL.prx");
                    MoveAsset("Assets/Plugins/PS4/libfmodstudio.prx", lib + "/ps4/libfmodstudio.prx");
                    MoveAsset("Assets/Plugins/PS4/libfmodstudioL.prx", lib + "/ps4/libfmodstudioL.prx");
                    MoveAsset("Assets/Plugins/PS4/resonanceaudio.prx", lib + "/ps4/resonanceaudio.prx");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/PS4") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/PS4" }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/PS4");
                }
                // Switch libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Switch"))
                {
                    MoveAsset("Assets/Plugins/Switch/libfmodstudiounityplugin.a", lib + "/switch/libfmodstudiounityplugin.a");
                    MoveAsset("Assets/Plugins/Switch/libfmodstudiounitypluginL.a", lib + "/switch/libfmodstudiounitypluginL.a");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/Switch") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/Switch" }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/Switch");
                }
                // Xbox One libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/XboxOne"))
                {
                    MoveAsset("Assets/Plugins/XboxOne/fmod.dll", lib + "/xboxone/fmod.dll");
                    MoveAsset("Assets/Plugins/XboxOne/fmodL.dll", lib + "/xboxone/fmodL.dll");
                    MoveAsset("Assets/Plugins/XboxOne/fmodstudio.dll", lib + "/xboxone/fmodstudio.dll");
                    MoveAsset("Assets/Plugins/XboxOne/fmodstudioL.dll", lib + "/xboxone/fmodstudioL.dll");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/XboxOne") && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/XboxOne" }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/XboxOne");
                }
                // Linux libs
                archs = new string[] { "x86", "x86_64" };
                foreach (string arch in archs)
                {
                    MoveAsset("Assets/Plugins/" + arch + "/libfmod.so", lib + "/linux/" + arch + "/libfmod.so");
                    MoveAsset("Assets/Plugins/" + arch + "/libfmodL.so", lib + "/linux/" + arch + "/libfmodL.so");
                    MoveAsset("Assets/Plugins/" + arch + "/libfmodstudio.so", lib + "/linux/" + arch + "/libfmodstudio.so");
                    MoveAsset("Assets/Plugins/" + arch + "/libfmodstudioL.so", lib + "/linux/" + arch + "/libfmodstudio.so");
                    MoveAsset("Assets/Plugins/" + arch + "/libgvraudio.so", lib + "/linux/" + arch + "/libgvraudio.so");
                    MoveAsset("Assets/Plugins/" + arch + "/libresonanceaudio.so", lib + "/linux/" + arch + "/libresonanceaudio.so");
                    // The folders will be deleted after the windows libs are moved.
                }
                // Windows libs
                foreach (string arch in archs)
                {
                    MoveAsset("Assets/Plugins/" + arch + "/fmodstudio.dll", lib + "/win/" + arch + "/fmodstudio.dll");
                    MoveAsset("Assets/Plugins/" + arch + "/fmodstudioL.dll", lib + "/win/" + arch + "/fmodstudioL.dll");
                    MoveAsset("Assets/Plugins/" + arch + "/gvraudio.dll", lib + "/win/" + arch + "/gvraudio.dll");
                    MoveAsset("Assets/Plugins/" + arch + "/resonanceaudio.dll", lib + "/win/" + arch + "/resonanceaudio.dll");
                    if (AssetDatabase.IsValidFolder("Assets/Plugins/" + arch) && AssetDatabase.FindAssets("", new string[] { "Assets/Plugins/" + arch }).Length == 0)
                        AssetDatabase.MoveAssetToTrash("Assets/Plugins/" + arch);
                }

                Debug.Log("Folder merge finished!");
            }
        }

        static void MoveAsset(string from, string to)
        {
            if (AssetDatabase.IsValidFolder(to))
            {
                // Need to move all sub files/folders manually
                string[] files = Directory.GetFiles(from, "*", SearchOption.TopDirectoryOnly);
                foreach (string fileName in files)
                {
                    AssetDatabase.MoveAsset(fileName, to + '/' + Path.GetFileName(fileName));
                }
                string[] directories = Directory.GetDirectories(from, "*", SearchOption.AllDirectories);
                foreach (string dir in directories)
                {
                    string subDir = dir.Replace(from, "");
                    files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);
                    foreach (string fileName in files)
                    {
                        AssetDatabase.MoveAsset(fileName, to + '/' + subDir + '/' + Path.GetFileName(fileName));
                    }
                }
            }
            else
            {
                string result = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(result))
                {
                    Debug.LogWarning("[FMOD] Failed to move " + from + " : " + result);
                }
            }
        }

        static List<FMOD.Studio.Bank> masterBanks = new List<FMOD.Studio.Bank>();
        static List<FMOD.Studio.Bank> previewBanks = new List<FMOD.Studio.Bank>();
        static FMOD.Studio.EventDescription previewEventDesc;
        static FMOD.Studio.EventInstance previewEventInstance;

        static PreviewState previewState;
        public static PreviewState PreviewState
        {
            get { return previewState; }
        }

        public static void PreviewEvent(EditorEventRef eventRef, Dictionary<string, float> previewParamValues)
        {
            bool load = true;
            if (previewEventDesc.isValid())
            {
                Guid guid;
                previewEventDesc.getID(out guid);
                if (guid == eventRef.Guid)
                {
                    load = false;
                }
                else
                {
                    PreviewStop();
                }
            }

            if (load)
            {
                masterBanks.Clear();
                previewBanks.Clear();

                foreach (EditorBankRef masterBankRef in EventManager.MasterBanks)
                {
                    FMOD.Studio.Bank masterBank;
                    CheckResult(System.loadBankFile(masterBankRef.Path, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out masterBank));
                    masterBanks.Add(masterBank);
                }

                if (!EventManager.MasterBanks.Exists(x => eventRef.Banks.Contains(x)))
                {
                    string bankName = eventRef.Banks[0].Name;
                    var banks = EventManager.Banks.FindAll(x => x.Name.Contains(bankName));
                    foreach (var bank in banks)
                    {
                        FMOD.Studio.Bank previewBank;
                        CheckResult(System.loadBankFile(bank.Path, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out previewBank));
                        previewBanks.Add(previewBank);
                    }
                }
                else
                {
                    foreach (var previewBank in previewBanks)
                    {
                        previewBank.clearHandle();
                    }
                }

                CheckResult(System.getEventByID(eventRef.Guid, out previewEventDesc));
                CheckResult(previewEventDesc.createInstance(out previewEventInstance));
            }

            foreach (EditorParamRef param in eventRef.Parameters)
            {
                FMOD.Studio.PARAMETER_DESCRIPTION paramDesc;
                CheckResult(previewEventDesc.getParameterDescriptionByName(param.Name, out paramDesc));
                param.ID = paramDesc.id;
                PreviewUpdateParameter(param.ID, previewParamValues[param.Name]);
            }

            CheckResult(previewEventInstance.start());
            previewState = PreviewState.Playing;
        }

        public static void PreviewUpdateParameter(FMOD.Studio.PARAMETER_ID id, float paramValue)
        {
            if (previewEventInstance.isValid())
            {
                CheckResult(previewEventInstance.setParameterByID(id, paramValue));
            }
        }

        public static void PreviewUpdatePosition(float distance, float orientation)
        {
            if (previewEventInstance.isValid())
            {
                // Listener at origin
                FMOD.ATTRIBUTES_3D pos = new FMOD.ATTRIBUTES_3D();
                pos.position.x = (float)Math.Sin(orientation) * distance;
                pos.position.y = (float)Math.Cos(orientation) * distance;
                pos.forward.x = 1.0f;
                pos.up.z = 1.0f;
                CheckResult(previewEventInstance.set3DAttributes(pos));
            }
        }

        public static void PreviewPause()
        {
            if (previewEventInstance.isValid())
            {
                bool paused;
                CheckResult(previewEventInstance.getPaused(out paused));
                CheckResult(previewEventInstance.setPaused(!paused));
                previewState = paused ? PreviewState.Playing : PreviewState.Paused;
            }
        }

        public static void PreviewStop()
        {
            if (previewEventInstance.isValid())
            {
                previewEventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                previewEventInstance.release();
                previewEventInstance.clearHandle();
                previewEventDesc.clearHandle();
                previewBanks.ForEach(x => { x.unload(); x.clearHandle(); });
                masterBanks.ForEach(x => { x.unload(); x.clearHandle(); });
                previewState = PreviewState.Stopped;
            }
        }

        public static float[] GetMetering()
        {
            FMOD.System lowlevel;
            CheckResult(System.getCoreSystem(out lowlevel));
            FMOD.ChannelGroup master;
            CheckResult(lowlevel.getMasterChannelGroup(out master));
            FMOD.DSP masterHead;
            CheckResult(master.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, out masterHead));

            FMOD.DSP_METERING_INFO outputMetering;
            CheckResult(masterHead.getMeteringInfo(IntPtr.Zero, out outputMetering));

            FMOD.SPEAKERMODE mode;
            int rate, raw;
            lowlevel.getSoftwareFormat(out rate, out mode, out raw);
            int channels;
            lowlevel.getSpeakerModeChannels(mode, out channels);

            float[] data = new float[outputMetering.numchannels > 0 ? outputMetering.numchannels : channels];
            if (outputMetering.numchannels > 0)
            {
                Array.Copy(outputMetering.rmslevel, data, outputMetering.numchannels);
            }
            return data;
        }


        const int StudioScriptPort = 3663;
        static NetworkStream networkStream = null;
        static Socket socket = null;
        static IAsyncResult socketConnection = null;

        static NetworkStream ScriptStream
        {
            get
            {
                if (networkStream == null)
                {
                    try
                    {
                        if (socket == null)
                        {
                            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        }

                        if (!socket.Connected)
                        {
                            socketConnection = socket.BeginConnect("127.0.0.1", StudioScriptPort, null, null);
                            socketConnection.AsyncWaitHandle.WaitOne();
                            socket.EndConnect(socketConnection);
                            socketConnection = null;
                        }

                        networkStream = new NetworkStream(socket);

                        byte[] headerBytes = new byte[128];
                        int read = ScriptStream.Read(headerBytes, 0, 128);
                        string header = Encoding.UTF8.GetString(headerBytes, 0, read - 1);
                        if (header.StartsWith("log():"))
                        {
                            UnityEngine.Debug.Log("FMOD Studio: Script Client returned " + header.Substring(6));
                        }    
                    }        
                    catch (Exception e)
                    {
                        UnityEngine.Debug.Log("FMOD Studio: Script Client failed to connect - Check FMOD Studio is running");

                        socketConnection = null;
                        socket = null;
                        networkStream = null;

                        throw e;
                    }
                }
                return networkStream;
            }
        }

        private static void AsyncConnectCallback(IAsyncResult result)
        {
            try
            {
                socket.EndConnect(result);
            }
            catch (Exception)
            {
            }
            finally
            {
                socketConnection = null;
            }
        }

        public static bool IsConnectedToStudio()
        {
            try
            {
                if (socket != null && socket.Connected)
                {
                    if (SendScriptCommand("true"))
                    {
                        return true;
                    }
                }

                if (socketConnection == null)
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socketConnection = socket.BeginConnect("127.0.0.1", StudioScriptPort, AsyncConnectCallback, null);
                }

                return false;

            }
            catch(Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public static bool SendScriptCommand(string command)
        {
            byte[] commandBytes = Encoding.UTF8.GetBytes(command);
            try
            {
                ScriptStream.Write(commandBytes, 0, commandBytes.Length);
                byte[] commandReturnBytes = new byte[128];
                int read = ScriptStream.Read(commandReturnBytes, 0, 128);
                string result = Encoding.UTF8.GetString(commandReturnBytes, 0, read - 1);
                return (result.Contains("true"));
            }
            catch (Exception)
            {
                if (networkStream != null)
                {
                    networkStream.Close();
                    networkStream = null;
                }
                return false;
            }
        }


        public static string GetScriptOutput(string command)
        {
            byte[] commandBytes = Encoding.UTF8.GetBytes(command);
            try
            {
                ScriptStream.Write(commandBytes, 0, commandBytes.Length);
                byte[] commandReturnBytes = new byte[2048];
                int read = ScriptStream.Read(commandReturnBytes, 0, commandReturnBytes.Length);
                string result = Encoding.UTF8.GetString(commandReturnBytes, 0, read - 1);
                if (result.StartsWith("out():"))
                {
                    return result.Substring(6).Trim();
                }
                return null;
            }
            catch (Exception)
            {
                networkStream.Close();
                networkStream = null;
                return null;
            }
        }

        public static bool IsFileOpenByStudio(string path)
        {
            bool open = true;
            try
            {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    open = false;
                }
            }
            catch (Exception)
            {
            }
            return open;
        }

        private static string GetMasterBank()
        {
            GetScriptOutput(string.Format("masterBankFolder = studio.project.workspace.masterBankFolder;"));
            string bankCountString = GetScriptOutput(string.Format("masterBankFolder.items.length;"));
            int bankCount = int.Parse(bankCountString);
            for (int i = 0; i < bankCount; i++)
            {
                string isMaster = GetScriptOutput(string.Format("masterBankFolder.items[{1}].isOfExactType(\"MasterBank\");", i));
                if (isMaster == "true")
                {
                    string guid = GetScriptOutput(string.Format("masterBankFolder.items[{1}].id;", i));
                    return guid;
                }
            }
            return "";
        }

        private static bool CheckForNameConflict(string folderGuid, string eventName)
        {
            GetScriptOutput(string.Format("nameConflict = false;"));
            GetScriptOutput(string.Format("checkFunction = function(val) {{ nameConflict |= val.name == \"{0}\"; }};", eventName));
            GetScriptOutput(string.Format("studio.project.lookup(\"{0}\").items.forEach(checkFunction, this); ", folderGuid));
            string conflictBool = GetScriptOutput(string.Format("nameConflict;"));
            return conflictBool == "1";
        }

        public static string CreateStudioEvent(string eventPath, string eventName)
        {
            var folders = eventPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string folderGuid = EditorUtils.GetScriptOutput("studio.project.workspace.masterEventFolder.id;");
            for (int i = 0; i < folders.Length; i++)
            {
                string parentGuid = folderGuid;
                GetScriptOutput(string.Format("guid = \"\";"));
                GetScriptOutput(string.Format("findFunc = function(val) {{ guid = val.isOfType(\"EventFolder\") && val.name == \"{0}\" ? val.id : guid; }};", folders[i]));
                GetScriptOutput(string.Format("studio.project.lookup(\"{0}\").items.forEach(findFunc, this);", folderGuid));
                folderGuid = GetScriptOutput(string.Format("guid;"));
                if (folderGuid == "")
                {
                    GetScriptOutput(string.Format("folder = studio.project.create(\"EventFolder\");"));
                    GetScriptOutput(string.Format("folder.name = \"{0}\"", folders[i]));
                    GetScriptOutput(string.Format("folder.folder = studio.project.lookup(\"{0}\");", parentGuid));
                    folderGuid = GetScriptOutput(string.Format("folder.id;"));
                }
            }

            if (CheckForNameConflict(folderGuid, eventName))
            {
                EditorUtility.DisplayDialog("Name Conflict", string.Format("The event {0} already exists under {1}", eventName, eventPath), "OK");
                return null;
            }

            GetScriptOutput("event = studio.project.create(\"Event\");");
            GetScriptOutput("event.note = \"Placeholder created via Unity\";");
            GetScriptOutput(string.Format("event.name = \"{0}\"", eventName));
            GetScriptOutput(string.Format("event.folder = studio.project.lookup(\"{0}\");", folderGuid));

            // Add a group track
            GetScriptOutput("track = studio.project.create(\"GroupTrack\");");
            GetScriptOutput("track.mixerGroup.output = event.mixer.masterBus;");
            GetScriptOutput("track.mixerGroup.name = \"Audio 1\";");
            GetScriptOutput("event.relationships.groupTracks.add(track);");

            // Add tags
            GetScriptOutput("tag = studio.project.create(\"Tag\");");
            GetScriptOutput("tag.name = \"placeholder\";");
            GetScriptOutput("tag.folder = studio.project.workspace.masterTagFolder;");
            GetScriptOutput("event.relationships.tags.add(tag);");

            string eventGuid = GetScriptOutput(string.Format("event.id;"));
            return eventGuid;
        }
    }
}