using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FMODUnity
{
    [AddComponentMenu("")]
    public class RuntimeManager : MonoBehaviour
    {
        static SystemNotInitializedException initException = null;
        static RuntimeManager instance;

        [SerializeField]
        FMODPlatform fmodPlatform;

        [AOT.MonoPInvokeCallback(typeof(FMOD.DEBUG_CALLBACK))]
        static FMOD.RESULT DEBUG_CALLBACK(FMOD.DEBUG_FLAGS flags, FMOD.StringWrapper file, int line, FMOD.StringWrapper func, FMOD.StringWrapper message)
        {
            if (flags == FMOD.DEBUG_FLAGS.ERROR)
            {
                Debug.LogError(string.Format(("[FMOD] {0} : {1}"), (string)func, (string)message));
            }
            else if (flags == FMOD.DEBUG_FLAGS.WARNING)
            {
                Debug.LogWarning(string.Format(("[FMOD] {0} : {1}"), (string)func, (string)message));
            }
            else if (flags == FMOD.DEBUG_FLAGS.LOG)
            {
                Debug.Log(string.Format(("[FMOD] {0} : {1}"), (string)func, (string)message));
            }
            return FMOD.RESULT.OK;
        }

        static RuntimeManager Instance
        {
            get
            {
                if (initException != null)
                {
                    throw initException;
                }

                if (instance == null)
                {
                    FMOD.RESULT initResult = FMOD.RESULT.OK; // Initialize can return an error code if it falls back to NO_SOUND, throw it as a non-cached exception

                    var existing = FindObjectsOfType(typeof(RuntimeManager)) as RuntimeManager[];
                    foreach (var iter in existing)
                    {
                        if (existing != null)
                        {
                            // Older versions of the integration may have leaked the runtime manager game object into the scene,
                            // which was then serialized. It won't have valid pointers so don't use it.
                            if (iter.cachedPointers[0] != 0)
                            {
                                instance = iter;
                                instance.studioSystem.handle = ((IntPtr)instance.cachedPointers[0]);
                                instance.coreSystem.handle = ((IntPtr)instance.cachedPointers[1]);
                            }
                            DestroyImmediate(iter);
                        }
                    }

                    var gameObject = new GameObject("FMOD.UnityIntegration.RuntimeManager");
                    instance = gameObject.AddComponent<RuntimeManager>();

                    if (Application.isPlaying) // This class is used in edit mode by the Timeline auditioning system
                    {
                        DontDestroyOnLoad(gameObject);
                    }
                    gameObject.hideFlags = HideFlags.HideAndDontSave;

                    try
                    {
                        #if UNITY_ANDROID && !UNITY_EDITOR
                        // First, obtain the current activity context
                        AndroidJavaObject activity = null;
                        using (var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        {
                            activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
                        }

                        using (var fmodJava = new AndroidJavaClass("org.fmod.FMOD"))
                        {
                            if (fmodJava != null)
                            {
                                fmodJava.CallStatic("init", activity);
                            }
                            else
                            {
                                UnityEngine.Debug.LogWarning("[FMOD] Cannot initialize Java wrapper");
                            }
                        }
                        #endif

                        RuntimeUtils.EnforceLibraryOrder();
                        initResult = instance.Initialize();
                    }
                    catch (Exception e)
                    {
                        initException = e as SystemNotInitializedException;
                        if (initException == null)
                        {
                            initException = new SystemNotInitializedException(e);
                        }
                        throw initException;
                    }

                    if (initResult != FMOD.RESULT.OK)
                    {
                        throw new SystemNotInitializedException(initResult, "Output forced to NO SOUND mode");
                    }
                }
                return instance;
            }
        }

        public static FMOD.Studio.System StudioSystem
        {
            get { return Instance.studioSystem; }
        }

        public static FMOD.System CoreSystem
        {
            get { return Instance.coreSystem; }
        }

        FMOD.Studio.System studioSystem;
        FMOD.System coreSystem;
        FMOD.DSP mixerHead;

        [SerializeField]
        private long[] cachedPointers = new long[2];

        struct LoadedBank
        {
            public FMOD.Studio.Bank Bank;
            public int RefCount;
        }

        Dictionary<string, LoadedBank> loadedBanks = new Dictionary<string, LoadedBank>();
        Dictionary<string, uint> loadedPlugins = new Dictionary<string, uint>();

        // Explicit comparer to avoid issues on platforms that don't support JIT compilation
        class GuidComparer : IEqualityComparer<Guid>
        {
            bool IEqualityComparer<Guid>.Equals(Guid x, Guid y)
            {
                return x.Equals(y);
            }

            int IEqualityComparer<Guid>.GetHashCode(Guid obj)
            {
                return obj.GetHashCode();
            }
        }
        Dictionary<Guid, FMOD.Studio.EventDescription> cachedDescriptions = new Dictionary<Guid, FMOD.Studio.EventDescription>(new GuidComparer());

        void CheckInitResult(FMOD.RESULT result, string cause)
        {
            if (result != FMOD.RESULT.OK)
            {
                if (studioSystem.isValid())
                {
                    studioSystem.release();
                    studioSystem.clearHandle();
                }
                throw new SystemNotInitializedException(result, cause);
            }
        }

        FMOD.RESULT Initialize()
        {
            #if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChange;
            #endif // UNITY_EDITOR

            FMOD.RESULT result = FMOD.RESULT.OK;
            FMOD.RESULT initResult = FMOD.RESULT.OK;
            Settings fmodSettings = Settings.Instance;
            fmodPlatform = RuntimeUtils.GetCurrentPlatform();

            int sampleRate = fmodSettings.GetSampleRate(fmodPlatform);
            int realChannels = Math.Min(fmodSettings.GetRealChannels(fmodPlatform), 256);
            int virtualChannels = fmodSettings.GetVirtualChannels(fmodPlatform);
            FMOD.SPEAKERMODE speakerMode = (FMOD.SPEAKERMODE)fmodSettings.GetSpeakerMode(fmodPlatform);
            FMOD.OUTPUTTYPE outputType = FMOD.OUTPUTTYPE.AUTODETECT;

            FMOD.ADVANCEDSETTINGS advancedSettings = new FMOD.ADVANCEDSETTINGS();
            advancedSettings.randomSeed = (uint)DateTime.Now.Ticks;
            #if UNITY_EDITOR || UNITY_STANDALONE
            advancedSettings.maxVorbisCodecs = realChannels;
            #elif UNITY_XBOXONE
            advancedSettings.maxXMACodecs = realChannels;
            #elif UNITY_PS4
            advancedSettings.maxAT9Codecs = realChannels;
            #else
            advancedSettings.maxFADPCMCodecs = realChannels;
            #endif

            SetThreadAffinity();

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            result = FMOD.Debug.Initialize(fmodSettings.LoggingLevel, FMOD.DEBUG_MODE.CALLBACK, DEBUG_CALLBACK, null);
            CheckInitResult(result, "FMOD.Debug.Initialize");
            #endif

            FMOD.Studio.INITFLAGS studioInitFlags = FMOD.Studio.INITFLAGS.NORMAL | FMOD.Studio.INITFLAGS.DEFERRED_CALLBACKS;
            if (fmodSettings.IsLiveUpdateEnabled(fmodPlatform))
            {
                studioInitFlags |= FMOD.Studio.INITFLAGS.LIVEUPDATE;
                advancedSettings.profilePort = fmodSettings.LiveUpdatePort;
            }

retry:
            result = FMOD.Studio.System.create(out studioSystem);
            CheckInitResult(result, "FMOD.Studio.System.create");

            result = studioSystem.getCoreSystem(out coreSystem);
            CheckInitResult(result, "FMOD.Studio.System.getCoreSystem");

            result = coreSystem.setOutput(outputType);
            CheckInitResult(result, "FMOD.System.setOutput");

            result = coreSystem.setSoftwareChannels(realChannels);
            CheckInitResult(result, "FMOD.System.setSoftwareChannels");

            result = coreSystem.setSoftwareFormat(sampleRate, speakerMode, 0);
            CheckInitResult(result, "FMOD.System.setSoftwareFormat");

            result = coreSystem.setAdvancedSettings(ref advancedSettings);
            CheckInitResult(result, "FMOD.System.setAdvancedSettings");

            if (!string.IsNullOrEmpty(Settings.Instance.EncryptionKey))
            {
                FMOD.Studio.ADVANCEDSETTINGS studioAdvancedSettings = new FMOD.Studio.ADVANCEDSETTINGS();
                result = studioSystem.setAdvancedSettings(studioAdvancedSettings, Settings.Instance.EncryptionKey);
                CheckInitResult(result, "FMOD.Studio.System.setAdvancedSettings");
            }

            result = studioSystem.initialize(virtualChannels, studioInitFlags, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
            if (result != FMOD.RESULT.OK && initResult == FMOD.RESULT.OK)
            {
                initResult = result; // Save this to throw at the end (we'll attempt NO SOUND to shield ourselves from unexpected device failures)
                outputType = FMOD.OUTPUTTYPE.NOSOUND;
                UnityEngine.Debug.LogErrorFormat("[FMOD] Studio::System::initialize returned {0}, defaulting to no-sound mode.", result.ToString());

                goto retry;
            }
            CheckInitResult(result, "Studio::System::initialize");

            // Test network functionality triggered during System::update
            if ((studioInitFlags & FMOD.Studio.INITFLAGS.LIVEUPDATE) != 0)
            {
                studioSystem.flushCommands(); // Any error will be returned through Studio.System.update

                result = studioSystem.update();
                if (result == FMOD.RESULT.ERR_NET_SOCKET_ERROR)
                {
                    studioInitFlags &= ~FMOD.Studio.INITFLAGS.LIVEUPDATE;
                    UnityEngine.Debug.LogWarning("[FMOD] Cannot open network port for Live Update (in-use), restarting with Live Update disabled.");

                    result = studioSystem.release();
                    CheckInitResult(result, "FMOD.Studio.System.Release");

                    goto retry;
                }
            }

            LoadPlugins(fmodSettings);
            LoadBanks(fmodSettings);

            return initResult;
        }

        class AttachedInstance
        {
            public FMOD.Studio.EventInstance instance;
            public Transform transform;
            public Rigidbody rigidBody;
            public Rigidbody2D rigidBody2D;
        }

        List<AttachedInstance> attachedInstances = new List<AttachedInstance>(128);

        #if UNITY_EDITOR
        List<FMOD.Studio.EventInstance> eventPositionWarnings = new List<FMOD.Studio.EventInstance>();
        #endif

        public static bool AddListener(int index)
        {
            if (HasListener[index])
            {
                // Listener already registered
                Debug.LogError(string.Format(("[FMOD] Listener with index {0} already registered."), index));
                return false;
            }
            HasListener[index] = true;
            numListeners = RecalculateTotalListeners();
            StudioSystem.setNumListeners(numListeners);
            return true;
        }

        public static bool RemoveListener(int index)
        {
            if (index != -1 && HasListener[index])
            {
                HasListener[index] = false;
                numListeners = RecalculateTotalListeners();

                if (StudioSystem.isValid())
                {
                    StudioSystem.setNumListeners(Math.Max(numListeners, 1));
                    return true;
                }
            }
            if (numListeners <= 0)
            {
                Debug.LogWarning("[FMOD] No Listeners currently assigned.");
            }
            return false;
        }

        private static int RecalculateTotalListeners()
        {
            int highestListenerNum = 0;
            for (int i = HasListener.Length; i > 0; i--)
            {
                if (HasListener[i - 1])
                {
                    highestListenerNum = i;
                    break;
                }
            }
            return highestListenerNum;
        }

        bool listenerWarningIssued = false;
        void Update()
        {
            if (studioSystem.isValid())
            {
                if (numListeners <= 0 && !listenerWarningIssued)
                {
                    listenerWarningIssued = true;
                    UnityEngine.Debug.LogWarning("[FMOD] Please add an 'FMOD Studio Listener' component to your a camera in the scene for correct 3D positioning of sounds.");
                }

                for (int i = 0; i < attachedInstances.Count; i++)
                {
                    FMOD.Studio.PLAYBACK_STATE playbackState = FMOD.Studio.PLAYBACK_STATE.STOPPED;
                    attachedInstances[i].instance.getPlaybackState(out playbackState);
                    if (!attachedInstances[i].instance.isValid() || 
                        playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED ||
                        attachedInstances[i].transform == null // destroyed game object
                        )
                    {
                        attachedInstances.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (attachedInstances[i].rigidBody)
                    {
                        attachedInstances[i].instance.set3DAttributes(RuntimeUtils.To3DAttributes(attachedInstances[i].transform, attachedInstances[i].rigidBody));
                    }
                    else
                    {
                        attachedInstances[i].instance.set3DAttributes(RuntimeUtils.To3DAttributes(attachedInstances[i].transform, attachedInstances[i].rigidBody2D));
                    }
                }

                #if UNITY_EDITOR
                MuteAllEvents(UnityEditor.EditorUtility.audioMasterMute);

                for (int i = eventPositionWarnings.Count - 1; i >= 0; i--)
                {
                    if (eventPositionWarnings[i].isValid())
                    {
                        FMOD.ATTRIBUTES_3D attribs;
                        eventPositionWarnings[i].get3DAttributes(out attribs);
                        if (attribs.position.x == 1e+18F &&
                            attribs.position.y == 1e+18F &&
                            attribs.position.z == 1e+18F)
                        {
                            string path;
                            FMOD.Studio.EventDescription desc;
                            eventPositionWarnings[i].getDescription(out desc);
                            desc.getPath(out path);
                            Debug.LogWarningFormat("[FMOD] Instance of Event {0} has not had EventInstance.set3DAttributes() called on it yet!", path);
                        }
                    }
                    eventPositionWarnings.RemoveAt(i);
                }

                isOverlayEnabled = Settings.Instance.IsOverlayEnabled(fmodPlatform);
                #endif

                if (isOverlayEnabled)
                {
                    if (!overlayDrawer)
                    {
                        overlayDrawer = Instance.gameObject.AddComponent<FMODRuntimeManagerOnGUIHelper>();
                        overlayDrawer.TargetRuntimeManager = this;
                    }
                    else
                    {
                        overlayDrawer.gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (overlayDrawer != null && overlayDrawer.gameObject.activeSelf)
                    {
                        overlayDrawer.gameObject.SetActive(false);
                    }
                }

                studioSystem.update();
            }
        }

        public static void AttachInstanceToGameObject(FMOD.Studio.EventInstance instance, Transform transform, Rigidbody rigidBody)
        {
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform, rigidBody));
            var attachedInstance = new AttachedInstance();
            attachedInstance.transform = transform;
            attachedInstance.instance = instance;
            attachedInstance.rigidBody = rigidBody;
            Instance.attachedInstances.Add(attachedInstance);
        }

        public static void AttachInstanceToGameObject(FMOD.Studio.EventInstance instance, Transform transform, Rigidbody2D rigidBody2D)
        {
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform, rigidBody2D));
            var attachedInstance = new AttachedInstance();
            attachedInstance.transform = transform;
            attachedInstance.instance = instance;
            attachedInstance.rigidBody2D = rigidBody2D;
            attachedInstance.rigidBody = null;
            Instance.attachedInstances.Add(attachedInstance);
        }

        public static void DetachInstanceFromGameObject(FMOD.Studio.EventInstance instance)
        {
            var manager = Instance;
            for (int i = 0; i < manager.attachedInstances.Count; i++)
            {
                if (manager.attachedInstances[i].instance.handle == instance.handle)
                {
                    manager.attachedInstances.RemoveAt(i);
                    return;
                }
            }
        }

        protected bool isOverlayEnabled = false;
        FMODRuntimeManagerOnGUIHelper overlayDrawer = null;
        Rect windowRect = new Rect(10, 10, 300, 100);

        public void ExecuteOnGUI()
        {
            if (studioSystem.isValid() && isOverlayEnabled)
            {
                windowRect = GUI.Window(0, windowRect, DrawDebugOverlay, "FMOD Studio Debug");
            }
        }

        #if !UNITY_EDITOR
        private void Start()
        {
            isOverlayEnabled = Settings.Instance.IsOverlayEnabled(fmodPlatform);
        }
        #endif

        string lastDebugText;
        float lastDebugUpdate = 0;
        void DrawDebugOverlay(int windowID)
        {
            if (lastDebugUpdate + 0.25f < Time.unscaledTime)
            {
                if (initException != null)
                {
                    lastDebugText = initException.Message;
                }
                else
                {
                    if (!mixerHead.hasHandle())
                    {
                        FMOD.ChannelGroup master;
                        coreSystem.getMasterChannelGroup(out master);
                        master.getDSP(0, out mixerHead);
                        mixerHead.setMeteringEnabled(false, true);
                    }

                    StringBuilder debug = new StringBuilder();

                    FMOD.Studio.CPU_USAGE cpuUsage;
                    studioSystem.getCPUUsage(out cpuUsage);
                    debug.AppendFormat("CPU: dsp = {0:F1}%, studio = {1:F1}%\n", cpuUsage.dspusage, cpuUsage.studiousage);

                    int currentAlloc, maxAlloc;
                    FMOD.Memory.GetStats(out currentAlloc, out maxAlloc);
                    debug.AppendFormat("MEMORY: cur = {0}MB, max = {1}MB\n", currentAlloc >> 20, maxAlloc >> 20);

                    int realchannels, channels;
                    coreSystem.getChannelsPlaying(out channels, out realchannels);
                    debug.AppendFormat("CHANNELS: real = {0}, total = {1}\n", realchannels, channels);

                    FMOD.DSP_METERING_INFO outputMetering;
                    mixerHead.getMeteringInfo(IntPtr.Zero, out outputMetering);
                    float rms = 0;
                    for (int i = 0; i < outputMetering.numchannels; i++)
                    {
                        rms += outputMetering.rmslevel[i] * outputMetering.rmslevel[i];
                    }
                    rms = Mathf.Sqrt(rms / (float)outputMetering.numchannels);

                    float db = rms > 0 ? 20.0f * Mathf.Log10(rms * Mathf.Sqrt(2.0f)) : -80.0f;
                    if (db > 10.0f) db = 10.0f;

                    debug.AppendFormat("VOLUME: RMS = {0:f2}db\n", db);
                    lastDebugText = debug.ToString();
                    lastDebugUpdate = Time.unscaledTime;
                }
            }

            GUI.Label(new Rect(10, 20, 290, 100), lastDebugText);
            GUI.DragWindow();
        }

        void OnDisable()
        {
            // If we're being torn down for a script reload - cache the native pointers in something unity can serialize
            cachedPointers[0] = (long)studioSystem.handle;
            cachedPointers[1] = (long)coreSystem.handle;
        }

        void OnDestroy()
        {
            if (studioSystem.isValid())
            {
                studioSystem.release();
                studioSystem.clearHandle();
            }
            initException = null;
            instance = null;
        }

        #if UNITY_EDITOR
        public static void Destroy()
        {
            if (instance)
            {
                if (instance.studioSystem.isValid())
                {
                    instance.studioSystem.release();
                    instance.studioSystem.clearHandle();
                }
                DestroyImmediate(instance.gameObject);
                initException = null;
                instance = null;
            }
        }

        static void HandleBeforeAssemblyReload()
        {
            Destroy();
        }

        void HandlePlayModeStateChange(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
            {
                Destroy();
            }
        }
        #endif

        #if UNITY_IOS
        /* iOS alarm interruptions do not trigger OnApplicationPause
         * Sending the app to the background does trigger OnApplicationFocus
         * We don't want to use this on Android as other things (like the keyboard)
         * can steal focus.
         * https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnApplicationFocus.html */

        void OnApplicationFocus(bool focus)
        {
            if (studioSystem.isValid())
            {
                // Strings bank is always loaded
                if (loadedBanks.Count > 1)
                    PauseAllEvents(!focus);

                if (focus)
                {
                    coreSystem.mixerResume();
                }
                else
                {
                    coreSystem.mixerSuspend();
                }
            }
        }
        #else
        void OnApplicationPause(bool pauseStatus)
        {
            if (studioSystem.isValid())
            {
                PauseAllEvents(pauseStatus);

                if (pauseStatus)
                {
                    coreSystem.mixerSuspend();
                }
                else
                {
                    coreSystem.mixerResume();
                }
            }
        }
        #endif

        private void loadedBankRegister(LoadedBank loadedBank, string bankPath, string bankName, bool loadSamples, FMOD.RESULT loadResult)
        {
            if (loadResult == FMOD.RESULT.OK)
            {
                loadedBank.RefCount = 1;

                if (loadSamples)
                {
                    loadedBank.Bank.loadSampleData();
                }

                Instance.loadedBanks.Add(bankName, loadedBank);
            }
            else if (loadResult == FMOD.RESULT.ERR_EVENT_ALREADY_LOADED)
            {
                // someone loaded this bank directly using the studio API
                // TODO: will the null bank handle be an issue
                loadedBank.RefCount = 2;
                Instance.loadedBanks.Add(bankName, loadedBank);
            }
            else
            {
                throw new BankLoadException(bankPath, loadResult);
            }
        }

        #if UNITY_WEBGL
        IEnumerator loadFromWeb(string bankPath, string bankName, bool loadSamples)
        {
            byte[] loadWebResult;
            FMOD.RESULT loadResult;

            UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(bankPath);
            yield return www.SendWebRequest();
            loadWebResult = www.downloadHandler.data;

            LoadedBank loadedBank = new LoadedBank();
            loadResult = Instance.studioSystem.loadBankMemory(loadWebResult, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank);
            if (loadResult != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogWarningFormat("[FMOD] loadFromWeb.  Path = {0}, result = {1}.", bankPath, loadResult);
            }
            loadedBankRegister(loadedBank, bankPath, bankName, loadSamples, loadResult);

            Debug.LogFormat("[FMOD] Finished loading {0}", bankPath);
        }
        #endif

        public static void LoadBank(string bankName, bool loadSamples = false)
        {
            if (Instance.loadedBanks.ContainsKey(bankName))
            {
                LoadedBank loadedBank = Instance.loadedBanks[bankName];
                loadedBank.RefCount++;

                if (loadSamples)
                {
                    loadedBank.Bank.loadSampleData();
                }
                Instance.loadedBanks[bankName] = loadedBank;
            }
            else
            {
                string bankPath = RuntimeUtils.GetBankPath(bankName);
                FMOD.RESULT loadResult;
                #if UNITY_ANDROID && !UNITY_EDITOR
                if (!bankPath.StartsWith("file:///android_asset"))
                {
                    using (var www = new WWW(bankPath))
                    {
                        while (!www.isDone) { }
                        if (!String.IsNullOrEmpty(www.error))
                        {
                            throw new BankLoadException(bankPath, www.error);
                        }
                        else
                        {
                            LoadedBank loadedBank = new LoadedBank();
                            loadResult = Instance.studioSystem.loadBankMemory(www.bytes, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank);
                            Instance.loadedBankRegister(loadedBank, bankPath, bankName, loadSamples, loadResult);
                        }
                    }
                }
                else
                #elif UNITY_WEBGL
                if (bankPath.Contains("://"))
                {
                    Instance.StartCoroutine(Instance.loadFromWeb(bankPath, bankName, loadSamples));
                }
                else
                #endif
                {
                    LoadedBank loadedBank = new LoadedBank();
                    loadResult = Instance.studioSystem.loadBankFile(bankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank);
                    Instance.loadedBankRegister(loadedBank, bankPath, bankName, loadSamples, loadResult);
                }
            }
        }

        public static void LoadBank(TextAsset asset, bool loadSamples = false)
        {
            string bankName = asset.name;
            if (Instance.loadedBanks.ContainsKey(bankName))
            {
                LoadedBank loadedBank = Instance.loadedBanks[bankName];
                loadedBank.RefCount++;

                if (loadSamples)
                {
                    loadedBank.Bank.loadSampleData();
                }
            }
            else
            {
                LoadedBank loadedBank = new LoadedBank();
                FMOD.RESULT loadResult = Instance.studioSystem.loadBankMemory(asset.bytes, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out loadedBank.Bank);

                if (loadResult == FMOD.RESULT.OK)
                {
                    loadedBank.RefCount = 1;
                    Instance.loadedBanks.Add(bankName, loadedBank);

                    if (loadSamples)
                    {
                        loadedBank.Bank.loadSampleData();
                    }
                }
                else if (loadResult == FMOD.RESULT.ERR_EVENT_ALREADY_LOADED)
                {
                    // someone loaded this bank directly using the studio API
                    // TODO: will the null bank handle be an issue
                    loadedBank.RefCount = 2;
                    Instance.loadedBanks.Add(bankName, loadedBank);
                }
                else
                {
                    throw new BankLoadException(bankName, loadResult);
                }
            }
        }

        private void LoadBanks(Settings fmodSettings)
        {
            if (fmodSettings.ImportType == ImportType.StreamingAssets)
            {
                // Always load strings bank
                try
                {
                    switch (fmodSettings.BankLoadType)
                    {
                        case BankLoadType.All:
                            foreach (string masterBankFileName in fmodSettings.MasterBanks)
                            {
                                LoadBank(masterBankFileName + ".strings", fmodSettings.AutomaticSampleLoading);
                                LoadBank(masterBankFileName, fmodSettings.AutomaticSampleLoading);
                            }

                            foreach (var bank in fmodSettings.Banks)
                            {
                                LoadBank(bank, fmodSettings.AutomaticSampleLoading);
                            }

                            WaitForAllLoads();
                            break;
                        case BankLoadType.Specified:
                            foreach (var bank in fmodSettings.BanksToLoad)
                            {
                                if (!string.IsNullOrEmpty(bank))
                                {
                                    LoadBank(bank, fmodSettings.AutomaticSampleLoading);
                                }
                            }

                            WaitForAllLoads();
                            break;
                        case BankLoadType.None:
                            break;
                        default:
                            break;
                    }
                }
                catch (BankLoadException e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public static void UnloadBank(string bankName)
        {
            LoadedBank loadedBank;
            if (Instance.loadedBanks.TryGetValue(bankName, out loadedBank))
            {
                loadedBank.RefCount--;
                if (loadedBank.RefCount == 0)
                {
                    loadedBank.Bank.unload();
                    Instance.loadedBanks.Remove(bankName);
                    return;
                }
                Instance.loadedBanks[bankName] = loadedBank;
            }
        }

        public static bool AnyBankLoading()
        {
            bool loading = false;
            foreach (LoadedBank bank in Instance.loadedBanks.Values)
            {
                FMOD.Studio.LOADING_STATE loadingState;
                bank.Bank.getSampleLoadingState(out loadingState);
                loading |= (loadingState == FMOD.Studio.LOADING_STATE.LOADING);
            }
            return loading;
        }

        public static void WaitForAllLoads()
        {
            Instance.studioSystem.flushSampleLoading();
        }

        public static Guid PathToGUID(string path)
        {
            Guid guid = Guid.Empty;
            if (path.StartsWith("{"))
            {
                FMOD.Studio.Util.parseID(path, out guid);
            }
            else
            {
                var result = Instance.studioSystem.lookupID(path, out guid);
                if (result == FMOD.RESULT.ERR_EVENT_NOTFOUND)
                {
                    throw new EventNotFoundException(path);
                }
            }
            return guid;
        }

        public static FMOD.Studio.EventInstance CreateInstance(string path)
        {
            try
            {
                return CreateInstance(PathToGUID(path));
            }
            catch(EventNotFoundException)
            {
                // Switch from exception with GUID to exception with path
                throw new EventNotFoundException(path);
            }
        }

        public static FMOD.Studio.EventInstance CreateInstance(Guid guid)
        {
            FMOD.Studio.EventDescription eventDesc = GetEventDescription(guid);
            FMOD.Studio.EventInstance newInstance;
            eventDesc.createInstance(out newInstance);

            #if UNITY_EDITOR
            bool is3D = false;
            eventDesc.is3D(out is3D);
            if (is3D)
            {
                // Set position to 1e+18F, set3DAttributes should be called by the dev after this.
                newInstance.set3DAttributes(RuntimeUtils.To3DAttributes(new Vector3(1e+18F, 1e+18F, 1e+18F)));
                instance.eventPositionWarnings.Add(newInstance);
            }
            #endif

            return newInstance;
        }

        public static void PlayOneShot(string path, Vector3 position = new Vector3())
        {
            try
            {
                PlayOneShot(PathToGUID(path), position);
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning("[FMOD] Event not found: " + path);
            }
        }

        public static void PlayOneShot(Guid guid, Vector3 position = new Vector3())
        {
            var instance = CreateInstance(guid);
            instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            instance.start();
            instance.release();
        }

        public static void PlayOneShotAttached(string path, GameObject gameObject)
        {
            try
            {
                PlayOneShotAttached(PathToGUID(path), gameObject);
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning("[FMOD] Event not found: " + path);
            }
        }

        public static void PlayOneShotAttached(Guid guid, GameObject gameObject)
        {
            var instance = CreateInstance(guid);
            AttachInstanceToGameObject(instance, gameObject.transform, gameObject.GetComponent<Rigidbody>());
            instance.start();
            instance.release();
        }

        public static FMOD.Studio.EventDescription GetEventDescription(string path)
        {
            try
            {
                return GetEventDescription(PathToGUID(path));
            }
            catch (EventNotFoundException)
            {
                throw new EventNotFoundException(path);
            }
        }

        public static FMOD.Studio.EventDescription GetEventDescription(Guid guid)
        {
            FMOD.Studio.EventDescription eventDesc;
            if (Instance.cachedDescriptions.ContainsKey(guid) && Instance.cachedDescriptions[guid].isValid())
            {
                eventDesc = Instance.cachedDescriptions[guid];
            }
            else
            {
                var result = Instance.studioSystem.getEventByID(guid, out eventDesc);

                if (result != FMOD.RESULT.OK)
                {
                    throw new EventNotFoundException(guid);
                }

                if (eventDesc.isValid())
                {
                    Instance.cachedDescriptions[guid] = eventDesc;
                }
            }
            return eventDesc;
        }

        public static bool[] HasListener = new bool[FMOD.CONSTANTS.MAX_LISTENERS];
        private static int numListeners = 0;

        public static void SetListenerLocation(GameObject gameObject, Rigidbody rigidBody = null)
        {
            Instance.studioSystem.setListenerAttributes(0, RuntimeUtils.To3DAttributes(gameObject, rigidBody));
        }
        
        public static void SetListenerLocation(GameObject gameObject, Rigidbody2D rigidBody2D)
        {
            Instance.studioSystem.setListenerAttributes(0, RuntimeUtils.To3DAttributes(gameObject, rigidBody2D));
        }

        public static void SetListenerLocation(Transform transform)
        {
            Instance.studioSystem.setListenerAttributes(0, transform.To3DAttributes());
        }

        public static void SetListenerLocation(int listenerIndex, GameObject gameObject, Rigidbody rigidBody = null)
        {
            Instance.studioSystem.setListenerAttributes(listenerIndex, RuntimeUtils.To3DAttributes(gameObject, rigidBody));
        }
        
        public static void SetListenerLocation(int listenerIndex, GameObject gameObject, Rigidbody2D rigidBody2D)
        {
            Instance.studioSystem.setListenerAttributes(listenerIndex, RuntimeUtils.To3DAttributes(gameObject, rigidBody2D));
        }

        public static void SetListenerLocation(int listenerIndex, Transform transform)
        {
            Instance.studioSystem.setListenerAttributes(listenerIndex, transform.To3DAttributes());
        }

        public static FMOD.Studio.Bus GetBus(string path)
        {
            FMOD.Studio.Bus bus;
            if (StudioSystem.getBus(path, out bus) != FMOD.RESULT.OK)
            {
                throw new BusNotFoundException(path);
            }
            return bus;
        }

        public static FMOD.Studio.VCA GetVCA(string path)
        {
            FMOD.Studio.VCA vca;
            if (StudioSystem.getVCA(path, out vca) != FMOD.RESULT.OK)
            {
                throw new VCANotFoundException(path);
            }
            return vca;
        }

        public static void PauseAllEvents(bool paused)
        {
            if (HasBanksLoaded)
            {
                FMOD.Studio.Bus masterBus;
                if (StudioSystem.getBus("bus:/", out masterBus) == FMOD.RESULT.OK)
                {
                    masterBus.setPaused(paused);
                }
            }
        }

        public static void MuteAllEvents(bool muted)
        {
            if (HasBanksLoaded)
            {
                FMOD.Studio.Bus masterBus;
                if (StudioSystem.getBus("bus:/", out masterBus) == FMOD.RESULT.OK)
                {
                    masterBus.setMute(muted);
                }
            }
        }

        public static bool IsInitialized
        {
            get
            {
                return instance != null && instance.studioSystem.isValid();
            }
        }

        public static bool HasBanksLoaded
        {
            get
            {
                return Instance.loadedBanks.Count > 1; 
            }
        }

        public static bool HasBankLoaded(string loadedBank)
        {
            return (instance.loadedBanks.ContainsKey(loadedBank));
        }

        private void LoadPlugins(Settings fmodSettings)
        {
            #if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
            FmodUnityNativePluginInit(coreSystem.handle);
            #else

            FMOD.RESULT result;
            foreach (var pluginName in fmodSettings.Plugins)
            {
                if (string.IsNullOrEmpty(pluginName))
                    continue;
                string pluginPath = RuntimeUtils.GetPluginPath(pluginName);
                uint handle;
                result = coreSystem.loadPlugin(pluginPath, out handle);
                #if UNITY_64 || UNITY_EDITOR_64
                // Add a "64" suffix and try again
                if (result == FMOD.RESULT.ERR_FILE_BAD || result == FMOD.RESULT.ERR_FILE_NOTFOUND)
                {
                    string pluginPath64 = RuntimeUtils.GetPluginPath(pluginName + "64");
                    result = coreSystem.loadPlugin(pluginPath64, out handle);
                }
                #endif
                CheckInitResult(result, String.Format("Loading plugin '{0}' from '{1}'", pluginName, pluginPath));
                loadedPlugins.Add(pluginName, handle);
            }
            #endif
        }

        private void SetThreadAffinity()
        {
            #if UNITY_PS4 && !UNITY_EDITOR
            FMOD.PS4.THREADAFFINITY affinity = new FMOD.PS4.THREADAFFINITY
            {
                mixer = FMOD.PS4.THREAD.CORE2,
                studioUpdate = FMOD.PS4.THREAD.CORE4,
                studioLoadBank = FMOD.PS4.THREAD.CORE4,
                studioLoadSample = FMOD.PS4.THREAD.CORE4
            };
            FMOD.RESULT result = FMOD.PS4.setThreadAffinity(ref affinity);
            CheckInitResult(result, "FMOD.PS4.setThreadAffinity");

            #elif UNITY_XBOXONE && !UNITY_EDITOR
            FMOD.XboxOne.THREADAFFINITY affinity = new FMOD.XboxOne.THREADAFFINITY
            {
                mixer = FMOD.XboxOne.THREAD.CORE2,
                studioUpdate = FMOD.XboxOne.THREAD.CORE4,
                studioLoadBank = FMOD.XboxOne.THREAD.CORE4,
                studioLoadSample = FMOD.XboxOne.THREAD.CORE4
            };
            FMOD.RESULT result = FMOD.XboxOne.setThreadAffinity(ref affinity);
            CheckInitResult(result, "FMOD.XboxOne.setThreadAffinity");

            #elif UNITY_SWITCH && !UNITY_EDITOR
            FMOD.Switch.THREADAFFINITY affinity = new FMOD.Switch.THREADAFFINITY
            {
                mixer = FMOD.Switch.THREAD.CORE2,
                studioUpdate = FMOD.Switch.THREAD.CORE2,
                studioLoadBank = FMOD.Switch.THREAD.CORE2,
                studioLoadSample = FMOD.Switch.THREAD.CORE2
            };
            FMOD.RESULT result = FMOD.Switch.setThreadAffinity(ref affinity);
            CheckInitResult(result, "FMOD.Switch.setThreadAffinity");
            #endif
        }

        #if (UNITY_IOS || UNITY_TVOS) && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern FMOD.RESULT FmodUnityNativePluginInit(IntPtr system);
        #endif
    }
}