using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build.Reporting;
#endif

namespace FMODUnity
{
    [InitializeOnLoad]
    public class EventManager : MonoBehaviour
    {
        const string CacheAssetName = "FMODStudioCache";
        const string CacheAssetFullName = "Assets/Plugins/FMOD/Resources/" + CacheAssetName + ".asset";
        static EventCache eventCache;

        const string StringBankExtension = "strings.bank";
        const string BankExtension = "bank";

        const int FilePollTimeSeconds = 5;
        
        // How many seconds to wait since last file activity to start the import
        const int CountdownTimerReset = 15 / FilePollTimeSeconds;

        static int countdownTimer;

#if UNITY_EDITOR
        [MenuItem("FMOD/Refresh Banks", priority = 1)]
        public static void RefreshBanks()
        {
            UpdateCache();
            countdownTimer = 0;
            UpdateCache();
        }
#endif

        static void ClearCache()
        {
            countdownTimer = CountdownTimerReset;
            eventCache.StringsBankWriteTime = DateTime.MinValue;
            eventCache.EditorBanks.Clear();
            eventCache.EditorEvents.Clear();
            eventCache.EditorParameters.Clear();
            eventCache.StringsBanks.Clear();
            eventCache.MasterBanks.Clear();
            if (Settings.Instance)
                Settings.Instance.BanksToLoad.Clear();
            OnCacheChange();
        }

        static public void UpdateCache()
        {
            // Deserialize the cache from the unity resources
            if (eventCache == null)
            {
                eventCache = AssetDatabase.LoadAssetAtPath(CacheAssetFullName, typeof(EventCache)) as EventCache;
                if (eventCache == null || eventCache.cacheVersion != EventCache.CurrentCacheVersion)
                {
                    UnityEngine.Debug.Log("FMOD Studio: Cannot find serialized event cache or cache in old format, creating new instance");
                    eventCache = ScriptableObject.CreateInstance<EventCache>();
                    eventCache.cacheVersion = EventCache.CurrentCacheVersion;
                    if (!Directory.Exists("Assets/Plugins/FMOD/Resources"))
                    {
                        AssetDatabase.CreateFolder("Assets/Plugins/FMOD", "Resources");
                    }
                    AssetDatabase.CreateAsset(eventCache, CacheAssetFullName);
                }
            }

            if (EditorUtils.GetBankDirectory() == null)
            {
                ClearCache();
                return;
            }

            string defaultBankFolder = null;
            
            if (!Settings.Instance.HasPlatforms)
            {
                defaultBankFolder = EditorUtils.GetBankDirectory();
            }
            else
            {
                FMODPlatform platform = RuntimeUtils.GetEditorFMODPlatform();
                if (platform == FMODPlatform.None)
                {
                    platform = FMODPlatform.PlayInEditor;
                }

                defaultBankFolder = Path.Combine(EditorUtils.GetBankDirectory(), Settings.Instance.GetBankPlatform(platform));
            }

            string[] bankPlatforms = EditorUtils.GetBankPlatforms();
            string[] bankFolders = new string[bankPlatforms.Length];
            for (int i = 0; i < bankPlatforms.Length; i++)
            {
                bankFolders[i] = Path.Combine(EditorUtils.GetBankDirectory(), bankPlatforms[i]);
            }

            List<string> stringBanks = new List<string>(0);
            try
            {
                var files = Directory.GetFiles(defaultBankFolder, "*." + StringBankExtension);
                stringBanks = new List<string>(files);
            }
            catch
            {
            }

            // Strip out OSX resource-fork files that appear on FAT32
            stringBanks.RemoveAll((x) => Path.GetFileName(x).StartsWith("._"));

            if (stringBanks.Count == 0)
            {
                bool wasValid = eventCache.StringsBankWriteTime != DateTime.MinValue;
                ClearCache();
                if (wasValid)
                {
                    UnityEngine.Debug.LogError(string.Format("FMOD Studio: Directory {0} doesn't contain any banks. Build from the tool or check the path in the settings", defaultBankFolder));
                }
                return;
            }

            // If we have multiple .strings.bank files find the most recent
            stringBanks.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

            // Use the most recent string bank timestamp as a marker for the most recent build of any bank because it gets exported every time
            DateTime lastWriteTime = File.GetLastWriteTime(stringBanks[0]);

            if (lastWriteTime == eventCache.StringsBankWriteTime)
            {
                countdownTimer = CountdownTimerReset;
                return;
            }

            if (EditorUtils.IsFileOpenByStudio(stringBanks[0]))
            {
                countdownTimer = CountdownTimerReset;
                return;
            }

            // Most recent strings bank is newer than last cache update time, recache.

            // Get a list of all banks
            List<string> bankFileNames = new List<string>();
            List<string> reducedStringBanksList = new List<string>();
            HashSet<Guid> stringBankGuids = new HashSet<Guid>();

            foreach (string stringBankPath in stringBanks)
            {
                FMOD.Studio.Bank stringBank;
                EditorUtils.CheckResult(EditorUtils.System.loadBankFile(stringBankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out stringBank));

                if (!stringBank.isValid())
                {
                    countdownTimer = CountdownTimerReset;
                    return;
                }
                else
                {
                    // Unload the strings bank
                    stringBank.unload();
                }
                Guid stringBankGuid;
                EditorUtils.CheckResult(stringBank.getID(out stringBankGuid));

                if (!stringBankGuids.Add(stringBankGuid))
                {
                    // If we encounter multiple string banks with the same GUID then only use the first. This handles the scenario where
                    // a Studio project is cloned and extended for DLC with a new master bank name.
                    continue;
                }

                reducedStringBanksList.Add(stringBankPath);
            }

            bankFileNames = new List<string>(Directory.GetFiles(defaultBankFolder, "*.bank", SearchOption.AllDirectories));

            stringBanks = reducedStringBanksList;

            if (!UnityEditorInternal.InternalEditorUtility.inBatchMode)
            {
                // Check if any of the files are still being written by studio
                foreach (string bankFileName in bankFileNames)
                {
                    EditorBankRef bankRef = eventCache.EditorBanks.Find((x) => bankFileName == x.Path);
                    if (bankRef == null)
                    {
                        if (EditorUtils.IsFileOpenByStudio(bankFileName))
                        {
                            countdownTimer = CountdownTimerReset;
                            return;
                        }
                        continue;
                    }

                    if (bankRef.LastModified != File.GetLastWriteTime(bankFileName))
                    {
                        if (EditorUtils.IsFileOpenByStudio(bankFileName))
                        {
                            countdownTimer = CountdownTimerReset;
                            return;
                        }
                    }
                }

                // Count down the timer in case we catch studio in-between updating two files.
                if (countdownTimer-- > 0)
                {
                    return;
                }
            }

            eventCache.StringsBankWriteTime = lastWriteTime;

            // All files are finished being modified by studio so update the cache

            // Stop editor preview so no stale data being held
            EditorUtils.PreviewStop();

            // Reload the strings banks
            List<FMOD.Studio.Bank> loadedStringsBanks = new List<FMOD.Studio.Bank>();

            try
            {
                AssetDatabase.StartAssetEditing();

                eventCache.EditorBanks.ForEach((x) => x.Exists = false);
                HashSet<string> masterBankFileNames = new HashSet<string>();

                foreach (string stringBankPath in stringBanks)
                {
                    FMOD.Studio.Bank stringBank;
                    EditorUtils.CheckResult(EditorUtils.System.loadBankFile(stringBankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out stringBank));

                    if (!stringBank.isValid())
                    {
                        ClearCache();
                        return;
                    }

                    loadedStringsBanks.Add(stringBank);
                
                    FileInfo stringBankFileInfo = new FileInfo(stringBankPath);

                    string masterBankFileName = Path.GetFileName(stringBankPath).Replace(StringBankExtension, BankExtension);
                    masterBankFileNames.Add(masterBankFileName);

                    EditorBankRef stringsBankRef = eventCache.StringsBanks.Find(x => stringBankPath == x.Path);

                    if (stringsBankRef == null)
                    {
                        stringsBankRef = ScriptableObject.CreateInstance<EditorBankRef>();
                        stringsBankRef.FileSizes = new List<EditorBankRef.NameValuePair>();
                        eventCache.EditorBanks.Add(stringsBankRef);
                        AssetDatabase.AddObjectToAsset(stringsBankRef, eventCache);
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(stringsBankRef));
                        eventCache.StringsBanks.Add(stringsBankRef);
                    }

                    stringsBankRef.Path = stringBankPath;
                    stringsBankRef.LastModified = stringBankFileInfo.LastWriteTime;
                    stringsBankRef.Exists = true;
                    stringsBankRef.FileSizes.Clear();

                    if (Settings.Instance.HasPlatforms)
                    {
                        for (int i = 0; i < bankPlatforms.Length; i++)
                        {
                            stringsBankRef.FileSizes.Add(new EditorBankRef.NameValuePair(bankPlatforms[i], stringBankFileInfo.Length));
                        }
                    }
                    else
                    {
                        stringsBankRef.FileSizes.Add(new EditorBankRef.NameValuePair("", stringBankFileInfo.Length));
                    }
                }

                eventCache.EditorParameters.ForEach((x) => x.Exists = false);
                foreach (string bankFileName in bankFileNames)
                {
                    FileInfo bankFileInfo = new FileInfo(bankFileName);
                    EditorBankRef bankRef = eventCache.EditorBanks.Find((x) => bankFileInfo.FullName == x.Path);

                    // New bank we've never seen before
                    if (bankRef == null)
                    {
                        bankRef = ScriptableObject.CreateInstance<EditorBankRef>();
                        AssetDatabase.AddObjectToAsset(bankRef, eventCache);
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(bankRef));
                        bankRef.Path = bankFileInfo.FullName;
                        bankRef.LastModified = DateTime.MinValue;
                        bankRef.FileSizes = new List<EditorBankRef.NameValuePair>();
                        eventCache.EditorBanks.Add(bankRef);
                    }

                    bankRef.Exists = true;

                    // Timestamp check - if it doesn't match update events from that bank
                    if (bankRef.LastModified != bankFileInfo.LastWriteTime)
                    {
                        bankRef.LastModified = bankFileInfo.LastWriteTime;
                        UpdateCacheBank(bankRef);
                    }

                    // Update file sizes
                    bankRef.FileSizes.Clear();
                    if (Settings.Instance.HasPlatforms)
                    {
                        for (int i = 0; i < bankPlatforms.Length; i++)
                        {
                            string platformBankPath = Path.Combine(bankFolders[i], bankFileName);
                            var fileInfo = new FileInfo(platformBankPath);
                            if (fileInfo.Exists)
                            {
                                bankRef.FileSizes.Add(new EditorBankRef.NameValuePair(bankPlatforms[i], fileInfo.Length));
                            }
                        }
                    }
                    else
                    {
                        string platformBankPath = Path.Combine(EditorUtils.GetBankDirectory(), bankFileName);
                        var fileInfo = new FileInfo(platformBankPath);
                        if (fileInfo.Exists)
                        {
                            bankRef.FileSizes.Add(new EditorBankRef.NameValuePair("", fileInfo.Length));
                        }
                    }

                    if (masterBankFileNames.Contains(bankFileInfo.Name))
                    {
                        if (!eventCache.MasterBanks.Exists(x => bankFileInfo.FullName == x.Path))
                        {
                            eventCache.MasterBanks.Add(bankRef);
                        }
                    }
                }

                // Remove any stale entries from bank, event and parameter lists
                eventCache.EditorBanks.FindAll((x) => !x.Exists).ForEach(RemoveCacheBank);
                eventCache.EditorBanks.RemoveAll((x) => !x.Exists);
                eventCache.EditorEvents.RemoveAll((x) => x.Banks.Count == 0);
                eventCache.EditorParameters.RemoveAll((x) => !x.Exists);
                eventCache.MasterBanks.RemoveAll((x) => !x.Exists);
                eventCache.StringsBanks.RemoveAll((x) => !x.Exists);

                OnCacheChange();
            }
            finally
            {
                // Unload the strings banks
                loadedStringsBanks.ForEach(x => x.unload());
                AssetDatabase.StopAssetEditing();
            }
        }

        static void UpdateCacheBank(EditorBankRef bankRef)
        {
            // Clear out any cached events from this bank
            eventCache.EditorEvents.ForEach((x) => x.Banks.Remove(bankRef));

            FMOD.Studio.Bank bank;
            bankRef.LoadResult = EditorUtils.System.loadBankFile(bankRef.Path, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out bank);

            if (bankRef.LoadResult == FMOD.RESULT.ERR_EVENT_ALREADY_LOADED)
            {
                EditorUtils.System.getBank(bankRef.Name, out bank);
                bank.unload();
                bankRef.LoadResult = EditorUtils.System.loadBankFile(bankRef.Path, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out bank);
            }

            if (bankRef.LoadResult == FMOD.RESULT.OK)
            {
                // Iterate all events in the bank and cache them
                FMOD.Studio.EventDescription[] eventList;
                var result = bank.getEventList(out eventList);
                if (result == FMOD.RESULT.OK)
                {
                    foreach (var eventDesc in eventList)
                    {
                        string path;
                        result = eventDesc.getPath(out path);
                        EditorEventRef eventRef = eventCache.EditorEvents.Find((x) => x.Path == path);
                        if (eventRef == null)
                        {
                            eventRef = ScriptableObject.CreateInstance<EditorEventRef>();
                            AssetDatabase.AddObjectToAsset(eventRef, eventCache);
                            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(eventRef));
                            eventRef.Banks = new List<EditorBankRef>();
                            eventCache.EditorEvents.Add(eventRef);
                        }

                        eventRef.Banks.Add(bankRef);
                        Guid guid;
                        eventDesc.getID(out guid);
                        eventRef.Guid = guid;
                        eventRef.Path = path;
                        eventDesc.is3D(out eventRef.Is3D);
                        eventDesc.isOneshot(out eventRef.IsOneShot);
                        eventDesc.isStream(out eventRef.IsStream);
                        eventDesc.getMaximumDistance(out eventRef.MaxDistance);
                        eventDesc.getMinimumDistance(out eventRef.MinDistance);
                        eventDesc.getLength(out eventRef.Length);
                        int paramCount = 0;
                        eventDesc.getParameterDescriptionCount(out paramCount);
                        eventRef.Parameters = new List<EditorParamRef>(paramCount);
                        for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                        {
                            FMOD.Studio.PARAMETER_DESCRIPTION param;
                            eventDesc.getParameterDescriptionByIndex(paramIndex, out param);
                            if ((param.flags & FMOD.Studio.PARAMETER_FLAGS.READONLY) != 0)
                            {
                                continue;
                            }
                            EditorParamRef paramRef = ScriptableObject.CreateInstance<EditorParamRef>();
                            AssetDatabase.AddObjectToAsset(paramRef, eventCache);
                            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(paramRef));
                            paramRef.Name = param.name;
                            paramRef.Min = param.minimum;
                            paramRef.Max = param.maximum;
                            paramRef.Default = param.defaultvalue;
                            eventRef.Parameters.Add(paramRef);
                        }
                    }
                }

                // Update global parameter list for each bank
                FMOD.Studio.PARAMETER_DESCRIPTION[] parameterDescriptions;
                result = EditorUtils.System.getParameterDescriptionList(out parameterDescriptions);
                if (result == FMOD.RESULT.OK)
                {
                    for (int i = 0; i < parameterDescriptions.Length; i++)
                    {
                        FMOD.Studio.PARAMETER_DESCRIPTION param = parameterDescriptions[i];
                        if (param.flags == FMOD.Studio.PARAMETER_FLAGS.GLOBAL)
                        {
                            EditorParamRef paramRef = eventCache.EditorParameters.Find((x) =>
                                (parameterDescriptions[i].id.data1 == x.ID.data1 && param.id.data2 == x.ID.data2));
                            if (paramRef == null)
                            {
                                paramRef = ScriptableObject.CreateInstance<EditorParamRef>();
                                AssetDatabase.AddObjectToAsset(paramRef, eventCache);
                                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(paramRef));
                                eventCache.EditorParameters.Add(paramRef);
                                paramRef.ID = param.id;
                            }
                            paramRef.Name = paramRef.name = param.name;
                            paramRef.Min = param.minimum;
                            paramRef.Max = param.maximum;
                            paramRef.Default = param.defaultvalue;
                            paramRef.Exists = true;
                        }
                    }
                }
                bank.unload();
            }
            else
            {
                Debug.LogError(string.Format("FMOD Studio: Unable to load {0}: {1}", bankRef.Name, FMOD.Error.String(bankRef.LoadResult)));
                eventCache.StringsBankWriteTime = DateTime.MinValue;
            }
        }

        static void RemoveCacheBank(EditorBankRef bankRef)
        {
            eventCache.EditorEvents.ForEach((x) => x.Banks.Remove(bankRef));
        }

        static EventManager()
        {
            countdownTimer = CountdownTimerReset;
            EditorApplication.update += Update;
        }

        public static void CopyToStreamingAssets()
        {
            FMODPlatform platform = RuntimeUtils.GetEditorFMODPlatform();
            if (platform == FMODPlatform.None)
            {
                UnityEngine.Debug.LogWarning(string.Format("FMOD Studio: copy banks for platform {0} : Unsupported platform", EditorUserBuildSettings.activeBuildTarget.ToString()));
                return;
            }

            string bankTargetFolder =
                Settings.Instance.ImportType == ImportType.StreamingAssets
                ? Application.dataPath + "/StreamingAssets"
                : Application.dataPath + Path.DirectorySeparatorChar + Settings.Instance.TargetAssetPath;
            Directory.CreateDirectory(bankTargetFolder);

            string bankTargetExension =
                Settings.Instance.ImportType == ImportType.StreamingAssets
                ? "bank"
                : "bytes";

            string bankSourceFolder = EditorUtils.GetBankDirectory() + Path.DirectorySeparatorChar + Settings.Instance.GetBankPlatform(platform);

            if (Path.GetFullPath(bankTargetFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant() ==
                Path.GetFullPath(bankSourceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant())
            {
                return;
            }

            bool madeChanges = false;

            try
            {
                // Clean out any stale .bank files
                string[] currentBankFiles = Directory.GetFiles(bankTargetFolder, "*." + bankTargetExension, SearchOption.AllDirectories);
                foreach (var bankFileName in currentBankFiles)
                {
                    string bankName = Path.GetFileNameWithoutExtension(bankFileName);
                    if (!eventCache.EditorBanks.Exists((x) => bankName == x.Name))
                    {
                        File.Delete(bankFileName);
                        madeChanges = true;
                    }
                }

                // Copy over any files that don't match timestamp or size or don't exist
                foreach (var bankRef in eventCache.EditorBanks)
                {
                    var subDir = Path.GetDirectoryName(bankRef.Path).Replace(bankSourceFolder, "");
                    string sourcePath = bankSourceFolder + subDir + Path.DirectorySeparatorChar + bankRef.Name + ".bank";
                    string targetPath = bankTargetFolder + subDir + Path.DirectorySeparatorChar + bankRef.Name + "." + bankTargetExension;

                    FileInfo sourceInfo = new FileInfo(sourcePath);
                    FileInfo targetInfo = new FileInfo(targetPath);

                    if (!targetInfo.Exists ||
                        sourceInfo.Length != targetInfo.Length ||
                        sourceInfo.LastWriteTime != targetInfo.LastWriteTime)
                    {
                        if (!targetInfo.Directory.Exists)
                        {
                            targetInfo.Directory.Create();
                        }
                        else if (targetInfo.Exists)
                        {
                            targetInfo.IsReadOnly = false;
                        }
                        File.Copy(sourcePath, targetPath, true);
                        targetInfo = new FileInfo(targetPath);
                        targetInfo.IsReadOnly = false;
                        targetInfo.LastWriteTime = sourceInfo.LastWriteTime;
                        
                        madeChanges = true;
                    }
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogError(string.Format("FMOD Studio: copy banks for platform {0} : copying banks from {1} to {2}", platform.ToString(), bankSourceFolder, bankTargetFolder));
                UnityEngine.Debug.LogException(exception);
                return;
            }

            if (madeChanges)
            {
                AssetDatabase.Refresh();
                UnityEngine.Debug.Log(string.Format("FMOD Studio: copy banks for platform {0} : copying banks from {1} to {2} succeeded", platform.ToString(), bankSourceFolder, bankTargetFolder));
            }
        }

        private static void BuildTargetChanged()
        {
            UpdateCache();

            // Copy over assets for the new platform
            CopyToStreamingAssets();
        }   

        static void OnCacheChange()
        {
            var settings = Settings.Instance;
            settings.MasterBanks.Clear();

            foreach (EditorBankRef bankRef in eventCache.MasterBanks)
            {
                settings.MasterBanks.Add(bankRef.Name);
            }

            settings.Banks.Clear();

            foreach (var bankRef in eventCache.EditorBanks)
            {
                if (!eventCache.MasterBanks.Contains(bankRef) &&
                    !eventCache.StringsBanks.Contains(bankRef))
                {
                    settings.Banks.Add(bankRef.Name);
                }
            }
            EditorUtility.SetDirty(settings);

            CopyToStreamingAssets();

            EventBrowser.RepaintEventBrowser();
        }

        static bool firstUpdate = true;
        static float lastCheckTime;
        static void Update()
        {
            if (firstUpdate)
            {
                UpdateCache();
                OnCacheChange();
                CopyToStreamingAssets();
                bool isValid;
                string validateMessage;
                EditorUtils.ValidateSource(out isValid, out validateMessage);
                if (!isValid)
                {
                    Debug.LogError("FMOD Studio: " + validateMessage);
                }
                firstUpdate = false;
                lastCheckTime = Time.realtimeSinceStartup;
            }

            if (lastCheckTime + FilePollTimeSeconds < Time.realtimeSinceStartup)
            {
                UpdateCache();
                lastCheckTime = Time.realtimeSinceStartup;
            }
        }

        public static List<EditorEventRef> Events
        {
            get
            {
                UpdateCache();
                return eventCache.EditorEvents;
            }
        }

        public static List<EditorBankRef> Banks
        {
            get
            {
                UpdateCache();
                return eventCache.EditorBanks;
            }
        }

        public static List<EditorParamRef> Parameters
        {
            get
            {
                UpdateCache();
                return eventCache.EditorParameters;
            }
        }

        public static List<EditorBankRef> MasterBanks
        { 
            get
            {
                UpdateCache();
                return eventCache.MasterBanks;
            }
        }

        public static bool IsLoaded
        {
            get
            {
                return EditorUtils.GetBankDirectory() != null;
            }
        }

        public static bool IsValid
        {
            get
            {
                UpdateCache();
                return eventCache.StringsBankWriteTime != DateTime.MinValue;
            }
        }

        public static EditorEventRef EventFromPath(string path)
        {
            UpdateCache();
            return eventCache.EditorEvents.Find((x) => x.Path.Equals(path, StringComparison.CurrentCultureIgnoreCase));
        }

        public static EditorEventRef EventFromGUID(Guid guid)
        {
            UpdateCache();
            return eventCache.EditorEvents.Find((x) => x.Guid == guid);
        }

        public static EditorParamRef ParamFromPath(string name)
        {
            UpdateCache();
            return eventCache.EditorParameters.Find((x) => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
        }

        public class ActiveBuildTargetListener : IActiveBuildTargetChanged
        {
            public int callbackOrder{ get { return 0; } }
            public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
            {
                BuildTargetChanged();
            }
        }
        #if UNITY_2018_1_OR_NEWER
        public class PreprocessBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder { get { return 0; } }
            public void OnPreprocessBuild(BuildReport report)
            {
                BuildTargetChanged();
            }
        }
        #else
        public class PreprocessBuild : IPreprocessBuild
        {
            public int callbackOrder { get { return 0; } }
            public void OnPreprocessBuild(BuildTarget target, string path)
            {
                BuildTargetChanged();
            }
        }
        #endif
    }
}
