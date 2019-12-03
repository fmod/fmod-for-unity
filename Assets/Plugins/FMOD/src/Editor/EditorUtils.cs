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
                    reason = "FMOD Studio Project path not set";
                    return;
                }
                if (!File.Exists(settings.SourceProjectPath))
                {
                    valid = false;
                    reason = "FMOD Studio Project not found";
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
                    reason = "FMOD Studio Project does not contain any built data. Please build your project in FMOD Studio.";
                    return;
                }
            }
            else
            {
                if (String.IsNullOrEmpty(settings.SourceBankPath))
                {
                    valid = false;
                    reason = "Build path not set";
                    return;
                }
                if (!Directory.Exists(settings.SourceBankPath))
                {
                    valid = false;
                    reason = "Build path doesn't exist";
                    return;
                }

                if (settings.HasPlatforms)
                {
                    if (Directory.GetDirectories(settings.SourceBankPath).Length == 0)
                    {
                        valid = false;
                        reason = "Build path doesn't contain any platform folders";
                        return;
                    }
                }
                else
                {
                    if (Directory.GetFiles(settings.SourceBankPath, "*.strings.bank").Length == 0)
                    {
                        valid = false;
                        reason = "Build path doesn't contain the contents of an FMOD Studio Build";
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

            EditorUtility.DisplayDialog("FMOD Studio Unity Integration", "Version: " + VerionNumberToString(version) + "\n\nCopyright \u00A9 Firelight Technologies Pty, Ltd. 2014-2019 \n\nSee LICENSE.TXT for additional license information.", "OK");
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
                if (AssetDatabase.IsValidFolder("Assets/Plugins/FMOD/Runtime") && AssetDatabase.FindAssets("Assets/Plugins/FMOD/Runtime").Length == 0)
                    Directory.Delete("Assets/Plugins/FMOD/Runtime");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Editor") && AssetDatabase.FindAssets("Assets/Plugins/Editor").Length == 0)
                    Directory.Delete("Assets/Plugins/Editor");
                // GoogleVR
                if (AssetDatabase.IsValidFolder("Assets/GoogleVR"))
                    MoveAsset("Assets/GoogleVR", addons + "/GoogleVR");
                // ResonanceAudio
                MoveAsset("Assets/ResonanceAudio", addons + "/ResonanceAudio");
                // Cache files
                MoveAsset("Assets/Resources", root + "/Resources");
                MoveAsset("Assets/FMODStudioCache.asset", root + "/Resources/FMODStudioCache.asset");
                if (AssetDatabase.IsValidFolder("Assets/Resources") && AssetDatabase.FindAssets("Assets/Resources").Length == 0)
                    Directory.Delete("Assets/Resources");
                // Android libs
                MoveAsset("Assets/Plugins/Android/libs/armeabi-v7a", lib + "/android/armeabi-v7a");
                MoveAsset("Assets/Plugins/Android/libs/x86", lib + "/android/x86");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Android/libs/arm68-v8a"))
                    MoveAsset("Assets/Plugins/Android/libs/arm68-v8a", lib + "/android/arm64-v8a");
                MoveAsset("Assets/Plugins/Android/fmod.jar", lib + "/android/fmod.jar");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Android") && AssetDatabase.FindAssets("Assets/Plugins/Android").Length == 0)
                    Directory.Delete("Assets/Plugins/Android", true);
                AssetDatabase.
                // Mac libs
                MoveAsset("Assets/Plugins/fmodstudio.bundle", lib + "/mac/fmodstudio.bundle");
                MoveAsset("Assets/Plugins/fmodstudioL.bundle", lib + "/mac/fmodstudioL.bundle");
                MoveAsset("Assets/Plugins/resonanceaudio.bundle", lib + "/mac/resonanceaudio.bundle");
                if (AssetDatabase.IsValidFolder("Assets/Plugins/gvraudio.bundle"))
                    MoveAsset("Assets/Plugins/gvraudio.bundle", lib + "/mac/gvraudio.bundle");
                // iOS libs
                MoveAsset("Assets/Plugins/iOS", lib + "/ios");
                // tvOS libs
                MoveAsset("Assets/Plugins/tvOS", lib + "/tvos");
                // UWP libs
                MoveAsset("Assets/Plugins/UWP", lib + "/uwp");
                // HTML5 libs
                MoveAsset("Assets/Plugins/WebGL", lib + "/html5");
                // PS4 libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/PS4"))
                    MoveAsset("Assets/Plugins/PS4", lib + "/ps4");
                // Switch libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/Switch"))
                    MoveAsset("Assets/Plugins/Switch", lib + "/switch");
                // Xbox One libs (optional)
                if (AssetDatabase.IsValidFolder("Assets/Plugins/XboxOne"))
                    MoveAsset("Assets/Plugins/XboxOne", lib + "/xboxone");
                // Linux libs
                MoveAsset("Assets/Plugins/x86/libfmod.so", lib + "/linux/x86/libfmod.so");
                MoveAsset("Assets/Plugins/x86/libfmodL.so", lib + "/linux/x86/libfmodL.so");
                MoveAsset("Assets/Plugins/x86/libfmodstudio.so", lib + "/linux/x86/libfmodstudio.so");
                MoveAsset("Assets/Plugins/x86/libfmodstudioL.so", lib + "/linux/x86/libfmodstudioL.so");
                MoveAsset("Assets/Plugins/x86_64/libfmod.so", lib + "/linux/x86_64/libfmod.so");
                MoveAsset("Assets/Plugins/x86_64/libfmodL.so", lib + "/linux/x86_64/libfmodL.so");
                MoveAsset("Assets/Plugins/x86_64/libfmodstudio.so", lib + "/linux/x86_64/libfmodstudio.so");
                MoveAsset("Assets/Plugins/x86_64/libfmodstudioL.so", lib + "/linux/x86_64/libfmodstudioL.so");
                MoveAsset("Assets/Plugins/x86_64/libresonanceaudio.so", lib + "/linux/x86_64/libresonanceaudio.so");
                // Windows libs
                MoveAsset("Assets/Plugins/x86", lib + "/win/x86");
                MoveAsset("Assets/Plugins/x86_64", lib + "/win/x86_64");

                Debug.Log("Folder merge finished!");
            }
        }

        static void MoveAsset(string from, string to)
        {
            string result = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(result))
            {
                Debug.LogWarning("[FMOD] Failed to move " + from + " : " + result);
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