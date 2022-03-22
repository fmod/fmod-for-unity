using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using System.Reflection;
using System.Collections;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build.Reporting;
#endif

namespace FMODUnity
{
    [InitializeOnLoad]
    public class EventManager : MonoBehaviour
    {
        const string CacheAssetName = "FMODStudioCache";
        public const string CacheAssetFullName = "Assets/Plugins/FMOD/Cache/Editor/" + CacheAssetName + ".asset";
        static EventCache eventCache;

        const string StringBankExtension = "strings.bank";
        const string BankExtension = "bank";

#if UNITY_EDITOR
        [MenuItem("FMOD/Refresh Banks", priority = 1)]
        public static void RefreshBanks()
        {
            string result = UpdateCache();
            OnCacheChange();
            if (Settings.Instance.ImportType == ImportType.AssetBundle)
            {
                UpdateBankStubAssets(EditorUserBuildSettings.activeBuildTarget);
            }

            BankRefresher.HandleBankRefresh(result);
        }
#endif

        static void ClearCache()
        {
            eventCache.CacheTime = DateTime.MinValue;
            eventCache.EditorBanks.Clear();
            eventCache.EditorEvents.Clear();
            eventCache.EditorParameters.Clear();
            eventCache.StringsBanks.Clear();
            eventCache.MasterBanks.Clear();
            if (Settings.Instance && Settings.Instance.BanksToLoad != null)
                Settings.Instance.BanksToLoad.Clear();
        }

        static private void AffirmEventCache()
        {
            if (eventCache == null)
            {
                UpdateCache();
            }
        }

        static private string UpdateCache()
        {
            if (eventCache == null)
            {
                eventCache = AssetDatabase.LoadAssetAtPath(CacheAssetFullName, typeof(EventCache)) as EventCache;
                if (eventCache == null || eventCache.cacheVersion != EventCache.CurrentCacheVersion)
                {
                    Debug.Log("FMOD: Event cache is missing or in an old format; creating a new instance.");

                    eventCache = ScriptableObject.CreateInstance<EventCache>();
                    eventCache.cacheVersion = EventCache.CurrentCacheVersion;

                    Directory.CreateDirectory(Path.GetDirectoryName(CacheAssetFullName));
                    AssetDatabase.CreateAsset(eventCache, CacheAssetFullName);
                }
            }

            var settings = Settings.Instance;

            if (string.IsNullOrEmpty(settings.SourceBankPath))
            {
                ClearCache();
                return null;
            }

            string defaultBankFolder = null;

            if (!settings.HasPlatforms)
            {
                defaultBankFolder = settings.SourceBankPath;
            }
            else
            {
                Platform platform = settings.CurrentEditorPlatform;

                if (platform == settings.DefaultPlatform)
                {
                    platform = settings.PlayInEditorPlatform;
                }

                defaultBankFolder = RuntimeUtils.GetCommonPlatformPath(Path.Combine(settings.SourceBankPath, platform.BuildDirectory));
            }

            string[] bankPlatforms = EditorUtils.GetBankPlatforms();
            string[] bankFolders = new string[bankPlatforms.Length];
            for (int i = 0; i < bankPlatforms.Length; i++)
            {
                bankFolders[i] = RuntimeUtils.GetCommonPlatformPath(Path.Combine(settings.SourceBankPath, bankPlatforms[i]));
            }

            // Get all banks and set cache time to most recent write time
            List<string> bankFileNames = new List<string>(Directory.GetFiles(defaultBankFolder, "*.bank", SearchOption.AllDirectories));
            DateTime lastWriteTime = bankFileNames.Max(fileName => File.GetLastWriteTime(fileName));

            // Exit early if cache is up to date
            if (lastWriteTime == eventCache.CacheTime)
            {
                return null;
            }

            eventCache.CacheTime = lastWriteTime;

            // Remove string banks from list
            bankFileNames.RemoveAll(x => x.Contains(".strings"));

            List<string> stringBanks = new List<string>(0);
            try
            {
                var files = Directory.GetFiles(defaultBankFolder, "*." + StringBankExtension, SearchOption.AllDirectories);
                stringBanks = new List<string>(files);
            }
            catch
            {
            }

            // Strip out OSX resource-fork files that appear on FAT32
            stringBanks.RemoveAll((x) => Path.GetFileName(x).StartsWith("._"));

            if (stringBanks.Count == 0)
            {
                ClearCache();
                return string.Format("Directory {0} doesn't contain any banks.\nBuild the banks in Studio or check the path in the settings.", defaultBankFolder);
            }

            // Stop editor preview so no stale data being held
            EditorUtils.PreviewStop();

            bool reloadPreviewBanks = EditorUtils.PreviewBanksLoaded;
            if (reloadPreviewBanks)
            {
                EditorUtils.UnloadPreviewBanks();
            }

            List<string> reducedStringBanksList = new List<string>();
            HashSet<Guid> stringBankGuids = new HashSet<Guid>();

            foreach (string stringBankPath in stringBanks)
            {
                FMOD.Studio.Bank stringBank;
                EditorUtils.CheckResult(EditorUtils.System.loadBankFile(stringBankPath, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out stringBank));

                if (!stringBank.isValid())
                {
                    return string.Format("{0} is not a valid bank.", stringBankPath);
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

            stringBanks = reducedStringBanksList;

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
                        return string.Format("{0} is not a valid bank.", stringBankPath);
                    }

                    loadedStringsBanks.Add(stringBank);

                    FileInfo stringBankFileInfo = new FileInfo(stringBankPath);

                    string masterBankFileName = Path.GetFileName(stringBankPath).Replace(StringBankExtension, BankExtension);
                    masterBankFileNames.Add(masterBankFileName);

                    EditorBankRef stringsBankRef = eventCache.StringsBanks.Find(x => RuntimeUtils.GetCommonPlatformPath(stringBankPath) == x.Path);

                    if (stringsBankRef == null)
                    {
                        stringsBankRef = ScriptableObject.CreateInstance<EditorBankRef>();
                        stringsBankRef.FileSizes = new List<EditorBankRef.NameValuePair>();
                        AssetDatabase.AddObjectToAsset(stringsBankRef, eventCache);
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(stringsBankRef));
                        eventCache.EditorBanks.Add(stringsBankRef);
                        eventCache.StringsBanks.Add(stringsBankRef);
                    }

                    stringsBankRef.SetPath(stringBankPath, defaultBankFolder);
                    string studioPath;
                    stringBank.getPath(out studioPath);
                    stringsBankRef.SetStudioPath(studioPath);
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
                    EditorBankRef bankRef = eventCache.EditorBanks.Find((x) => RuntimeUtils.GetCommonPlatformPath(bankFileName) == x.Path);

                    // New bank we've never seen before
                    if (bankRef == null)
                    {
                        bankRef = ScriptableObject.CreateInstance<EditorBankRef>();
                        AssetDatabase.AddObjectToAsset(bankRef, eventCache);
                        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(bankRef));

                        bankRef.SetPath(bankFileName, defaultBankFolder);
                        bankRef.LastModified = DateTime.MinValue;
                        bankRef.FileSizes = new List<EditorBankRef.NameValuePair>();

                        eventCache.EditorBanks.Add(bankRef);
                    }

                    bankRef.Exists = true;

                    FileInfo bankFileInfo = new FileInfo(bankFileName);

                    // Update events from this bank if it has been modified,
                    // or it is a master bank (so that we get any global parameters)
                    if (bankRef.LastModified != bankFileInfo.LastWriteTime
                        || masterBankFileNames.Contains(Path.GetFileName(bankFileName)))
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
                            string platformBankPath = RuntimeUtils.GetCommonPlatformPath(bankFolders[i] + bankFileName.Replace(defaultBankFolder, ""));
                            var fileInfo = new FileInfo(platformBankPath);
                            if (fileInfo.Exists)
                            {
                                bankRef.FileSizes.Add(new EditorBankRef.NameValuePair(bankPlatforms[i], fileInfo.Length));
                            }
                        }
                    }
                    else
                    {
                        string platformBankPath = RuntimeUtils.GetCommonPlatformPath(Path.Combine(Settings.Instance.SourceBankPath, bankFileName));
                        var fileInfo = new FileInfo(platformBankPath);
                        if (fileInfo.Exists)
                        {
                            bankRef.FileSizes.Add(new EditorBankRef.NameValuePair("", fileInfo.Length));
                        }
                    }

                    if (masterBankFileNames.Contains(bankFileInfo.Name))
                    {
                        if (!eventCache.MasterBanks.Exists(x => RuntimeUtils.GetCommonPlatformPath(bankFileName) == x.Path))
                        {
                            eventCache.MasterBanks.Add(bankRef);
                        }
                    }
                }

                // Remove any stale entries from bank, event and parameter lists
                eventCache.EditorBanks.FindAll((bankRef) => !bankRef.Exists).ForEach((bankRef) =>
                {
                    eventCache.EditorEvents.ForEach((eventRef) => eventRef.Banks.Remove(bankRef));
                    DestroyImmediate(bankRef, true);
                });
                eventCache.EditorBanks.RemoveAll((x) => x == null);
                eventCache.MasterBanks.RemoveAll((x) => x == null);
                eventCache.StringsBanks.RemoveAll((x) => x == null);

                eventCache.EditorEvents.FindAll((eventRef) => eventRef.Banks.Count == 0).ForEach((eventRef) =>
                {
                    eventRef.Parameters.ForEach((paramRef) => DestroyImmediate(paramRef, true));
                    DestroyImmediate(eventRef, true);
                });
                eventCache.EditorEvents.RemoveAll((x) => x == null);

                eventCache.EditorParameters.FindAll((paramRef) => !paramRef.Exists).ForEach((paramRef) =>
                {
                    DestroyImmediate(paramRef, true);
                });
                eventCache.EditorParameters.RemoveAll((x) => x == null);

                AssetDatabase.SaveAssets();
            }
            finally
            {
                // Unload the strings banks
                loadedStringsBanks.ForEach(x => x.unload());
                AssetDatabase.StopAssetEditing();

                if (reloadPreviewBanks)
                {
                    EditorUtils.LoadPreviewBanks();
                }

                Debug.Log("FMOD: Cache updated.");
            }

            return null;
        }

        static void UpdateCacheBank(EditorBankRef bankRef)
        {
            // Clear out any cached events from this bank
            eventCache.EditorEvents.ForEach((x) => x.Banks.Remove(bankRef));

            FMOD.Studio.Bank bank; 
            FMOD.RESULT loadResult = EditorUtils.System.loadBankFile(bankRef.Path, FMOD.Studio.LOAD_BANK_FLAGS.NORMAL, out bank);

            if (loadResult == FMOD.RESULT.OK)
            {
                // Get studio path
                string studioPath;
                bank.getPath(out studioPath);
                bankRef.SetStudioPath(studioPath);

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
                            eventRef.Parameters = new List<EditorParamRef>();
                        }

                        eventRef.Banks.Add(bankRef);
                        Guid guid;
                        eventDesc.getID(out guid);
                        eventRef.Guid = guid;
                        eventRef.Path = eventRef.name = path;
                        eventDesc.is3D(out eventRef.Is3D);
                        eventDesc.isOneshot(out eventRef.IsOneShot);
                        eventDesc.isStream(out eventRef.IsStream);
                        eventDesc.getMaximumDistance(out eventRef.MaxDistance);
                        eventDesc.getMinimumDistance(out eventRef.MinDistance);
                        eventDesc.getLength(out eventRef.Length);
                        int paramCount = 0;
                        eventDesc.getParameterDescriptionCount(out paramCount);
                        eventRef.Parameters.ForEach((x) => x.Exists = false);
                        for (int paramIndex = 0; paramIndex < paramCount; paramIndex++)
                        {
                            FMOD.Studio.PARAMETER_DESCRIPTION param;
                            eventDesc.getParameterDescriptionByIndex(paramIndex, out param);
                            // Skip if readonly and not global
                            if ((param.flags & FMOD.Studio.PARAMETER_FLAGS.READONLY) != 0 && (param.flags & FMOD.Studio.PARAMETER_FLAGS.GLOBAL) == 0)
                            {
                                continue;
                            }
                            EditorParamRef paramRef = eventRef.Parameters.Find((x) => x.ID.Equals(param.id));
                            if (paramRef == null)
                            {
                                paramRef = ScriptableObject.CreateInstance<EditorParamRef>();
                                AssetDatabase.AddObjectToAsset(paramRef, eventCache);
                                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(paramRef));
                                eventRef.Parameters.Add(paramRef);
                            }

                            InitializeParamRef(paramRef, param);

                            paramRef.name = "parameter:/" + Path.GetFileName(path) + "/" + paramRef.Name;
                            paramRef.Exists = true;
                        }
                        eventRef.Parameters.FindAll((x) => !x.Exists).ForEach((x) => DestroyImmediate(x, true));
                        eventRef.Parameters.RemoveAll((x) => x == null);
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
                        if ((param.flags & FMOD.Studio.PARAMETER_FLAGS.GLOBAL) == FMOD.Studio.PARAMETER_FLAGS.GLOBAL)
                        {
                            EditorParamRef paramRef = eventCache.EditorParameters.Find((x) => x.ID.Equals(param.id));
                            if (paramRef == null)
                            {
                                paramRef = ScriptableObject.CreateInstance<EditorParamRef>();
                                AssetDatabase.AddObjectToAsset(paramRef, eventCache);
                                AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(paramRef));
                                eventCache.EditorParameters.Add(paramRef);
                            }

                            InitializeParamRef(paramRef, param);

                            paramRef.name = "parameter:/" + param.name;
                            paramRef.Exists = true;
                        }
                    }
                }
                bank.unload();
            }
            else
            {
                Debug.LogError(string.Format("FMOD Studio: Unable to load {0}: {1}", bankRef.Name, FMOD.Error.String(loadResult)));
                eventCache.CacheTime = DateTime.MinValue;
            }
        }

        static void InitializeParamRef(EditorParamRef paramRef, FMOD.Studio.PARAMETER_DESCRIPTION description)
        {
            paramRef.Name = description.name;
            paramRef.Min = description.minimum;
            paramRef.Max = description.maximum;
            paramRef.Default = description.defaultvalue;
            paramRef.ID = description.id;
            paramRef.IsGlobal = (description.flags & FMOD.Studio.PARAMETER_FLAGS.GLOBAL) != 0;

            if ((description.flags & FMOD.Studio.PARAMETER_FLAGS.DISCRETE) != 0)
            {
                paramRef.Type = ParameterType.Discrete;
            }
            else
            {
                paramRef.Type = ParameterType.Continuous;
            }
        }

        static EventManager()
        {
            BuildStatusWatcher.OnBuildStarted += () => {
                BuildTargetChanged();
                CopyToStreamingAssets(EditorUserBuildSettings.activeBuildTarget);
            };
            BuildStatusWatcher.OnBuildEnded += () => {
                UpdateBankStubAssets(EditorUserBuildSettings.activeBuildTarget);
            };
        }

        public static void Startup()
        {
            // Avoid throwing exceptions so we don't stop other startup code from running
            try
            {
                RefreshBanks();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void CheckValidEventRefs(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                MonoBehaviour[] allBehaviours = gameObject.GetComponentsInChildren<MonoBehaviour>();

                foreach (MonoBehaviour behaviour in allBehaviours)
                {
                    if (behaviour != null)
                    {
                        Type componentType = behaviour.GetType();

                        FieldInfo[] fields = componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        foreach (FieldInfo item in fields)
                        {
                            if (HasAttribute(item, typeof(EventRefAttribute)))
                            {
                                if (item.FieldType == typeof(string))
                                {
                                    string output = item.GetValue(behaviour) as string;

                                    if (!IsValidEventRef(output))
                                    {
                                        Debug.LogWarningFormat("FMOD Studio: Unable to find FMOD Event \"{0}\" in scene \"{1}\" at path \"{2}\" \n- check the FMOD Studio event paths are set correctly in the Unity editor", output, scene.name, GetGameObjectPath(behaviour.transform));
                                    }
                                }
                                else if (typeof(IEnumerable).IsAssignableFrom(item.FieldType))
                                {
                                    foreach (var listItem in (IEnumerable)item.GetValue(behaviour))
                                    {
                                        if (listItem.GetType() == typeof(string))
                                        {
                                            string listOutput = listItem as string;
                                            if (!IsValidEventRef(listOutput))
                                            {
                                                Debug.LogWarningFormat("FMOD Studio: Unable to find FMOD Event \"{0}\" in scene \"{1}\" at path \"{2}\" \n- check the FMOD Studio event paths are set correctly in the Unity editor", listOutput, scene.name, GetGameObjectPath(behaviour.transform));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static string GetGameObjectPath(Transform transform)
        {
            string objectPath = "/" + transform.name;
            while(transform.parent != null)
            {
                transform = transform.parent;
                objectPath = "/" + transform.name + objectPath;
            }
            return objectPath;
        }

        private static bool HasAttribute(MemberInfo provider, params Type[] attributeTypes)
        {
            Attribute[] allAttributes = Attribute.GetCustomAttributes(provider, typeof(Attribute), true);

            if (allAttributes.Length == 0)
            {
                return false;
            }
            return allAttributes.Where(a => attributeTypes.Any(x => a.GetType() == x || x.IsAssignableFrom(a.GetType()))).Any();
        }

        private static bool IsValidEventRef(string reference)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return true;
            }
            EditorEventRef eventRef = EventManager.EventFromPath(reference);
            return eventRef != null;
        }

        private const string FMODLabel = "FMOD";

        public static void CopyToStreamingAssets(BuildTarget buildTarget)
        {
            if (Settings.Instance.ImportType == ImportType.AssetBundle && BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            if (string.IsNullOrEmpty(Settings.Instance.SourceBankPath))
                return;

            Platform platform = Settings.Instance.GetPlatform(buildTarget);

            if (platform == Settings.Instance.DefaultPlatform)
            {
                Debug.LogWarningFormat("FMOD Studio: copy banks for platform {0} : Unsupported platform", buildTarget);
                return;
            }

            string bankTargetFolder =
                Settings.Instance.ImportType == ImportType.StreamingAssets
                ? Settings.Instance.TargetPath
                : Application.dataPath + (string.IsNullOrEmpty(Settings.Instance.TargetAssetPath) ? "" : '/' + Settings.Instance.TargetAssetPath);
            bankTargetFolder = RuntimeUtils.GetCommonPlatformPath(bankTargetFolder);
            Directory.CreateDirectory(bankTargetFolder);

            string bankTargetExtension =
                Settings.Instance.ImportType == ImportType.StreamingAssets
                ? ".bank"
                : ".bytes";

            string bankSourceFolder =
                Settings.Instance.HasPlatforms
                ? Settings.Instance.SourceBankPath + '/' + platform.BuildDirectory
                : Settings.Instance.SourceBankPath;
            bankSourceFolder = RuntimeUtils.GetCommonPlatformPath(bankSourceFolder);

            if (Path.GetFullPath(bankTargetFolder).TrimEnd('/').ToUpperInvariant() ==
                Path.GetFullPath(bankSourceFolder).TrimEnd('/').ToUpperInvariant())
            {
                return;
            }

            bool madeChanges = false;

            try
            {
                // Clean out any stale .bank files
                string[] existingBankFiles =
                    Directory.GetFiles(bankTargetFolder, "*" + bankTargetExtension, SearchOption.AllDirectories);

                foreach (string bankFilePath in existingBankFiles)
                {
                    string bankName = EditorBankRef.CalculateName(bankFilePath, bankTargetFolder);

                    if (!eventCache.EditorBanks.Exists(x => x.Name == bankName))
                    {
                        string assetPath = bankFilePath.Replace(Application.dataPath, AssetsFolderName);

                        if (AssetHasLabel(assetPath, FMODLabel))
                        {
                            AssetDatabase.MoveAssetToTrash(assetPath);
                            madeChanges = true;
                        }
                    }
                }

                // Copy over any files that don't match timestamp or size or don't exist
                foreach (var bankRef in eventCache.EditorBanks)
                {
                    string sourcePath = bankSourceFolder + "/" + bankRef.Name + ".bank";
                    string targetPathRelative = bankRef.Name + bankTargetExtension;
                    string targetPathFull = bankTargetFolder + "/" + targetPathRelative;

                    FileInfo sourceInfo = new FileInfo(sourcePath);
                    FileInfo targetInfo = new FileInfo(targetPathFull);

                    if (!targetInfo.Exists ||
                        sourceInfo.Length != targetInfo.Length ||
                        sourceInfo.LastWriteTime != targetInfo.LastWriteTime)
                    {
                        if (targetInfo.Exists)
                        {
                            targetInfo.IsReadOnly = false;
                        }
                        else
                        {
                            EnsureFoldersExist(targetPathRelative, bankTargetFolder);
                        }

                        File.Copy(sourcePath, targetPathFull, true);
                        targetInfo = new FileInfo(targetPathFull);
                        targetInfo.IsReadOnly = false;
                        targetInfo.LastWriteTime = sourceInfo.LastWriteTime;

                        madeChanges = true;

                        string assetString = targetPathFull.Replace(Application.dataPath, "Assets");
                        AssetDatabase.ImportAsset(assetString);
                        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetString);
                        AssetDatabase.SetLabels(obj, new string[] { FMODLabel });
                    }
                }

                RemoveEmptyFMODFolders(bankTargetFolder);
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("FMOD Studio: copy banks for platform {0} : copying banks from {1} to {2}",
                    platform.DisplayName, bankSourceFolder, bankTargetFolder);
                Debug.LogException(exception);
                return;
            }

            if (madeChanges)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.LogFormat("FMOD Studio: copy banks for platform {0} : copying banks from {1} to {2} succeeded",
                    platform.DisplayName, bankSourceFolder, bankTargetFolder);
            }
        }

        public static void UpdateBankStubAssets(BuildTarget buildTarget)
        {
            if (Settings.Instance.ImportType != ImportType.AssetBundle
                || string.IsNullOrEmpty(Settings.Instance.SourceBankPath))
            {
                return;
            }

            Platform platform = Settings.Instance.GetPlatform(buildTarget);

            if (platform == Settings.Instance.DefaultPlatform)
            {
                Debug.LogWarningFormat("FMOD: Updating bank stubs: Unsupported platform {0}", buildTarget);
                return;
            }

            string bankTargetFolder = Application.dataPath;
            
            if (!string.IsNullOrEmpty(Settings.Instance.TargetAssetPath))
            {
                bankTargetFolder += "/" + Settings.Instance.TargetAssetPath;
            }

            bankTargetFolder = RuntimeUtils.GetCommonPlatformPath(bankTargetFolder);

            string bankSourceFolder = Settings.Instance.SourceBankPath;

            if (Settings.Instance.HasPlatforms)
            {
                bankSourceFolder += "/" + platform.BuildDirectory;
            }

            bankSourceFolder = RuntimeUtils.GetCommonPlatformPath(bankSourceFolder);

            if (Path.GetFullPath(bankTargetFolder).TrimEnd('/').ToUpperInvariant() ==
                Path.GetFullPath(bankSourceFolder).TrimEnd('/').ToUpperInvariant())
            {
                return;
            }

            bool madeChanges = false;

            Directory.CreateDirectory(bankTargetFolder);

            try
            {
                const string BankAssetExtension = ".bytes";

                // Clean out any stale stubs
                string[] existingBankFiles =
                    Directory.GetFiles(bankTargetFolder, "*" + BankAssetExtension, SearchOption.AllDirectories);

                foreach (string bankFilePath in existingBankFiles)
                {
                    string bankName = EditorBankRef.CalculateName(bankFilePath, bankTargetFolder);

                    if (!eventCache.EditorBanks.Exists(x => x.Name == bankName))
                    {
                        string assetPath = bankFilePath.Replace(Application.dataPath, AssetsFolderName);

                        if (AssetHasLabel(assetPath, FMODLabel))
                        {
                            AssetDatabase.MoveAssetToTrash(assetPath);
                            madeChanges = true;
                        }
                    }
                }

                // Create any stubs that don't exist, and ensure any that do exist have the correct data
                foreach (var bankRef in eventCache.EditorBanks)
                {
                    string sourcePath = bankSourceFolder + "/" + bankRef.Name + ".bank";
                    string targetPathRelative = bankRef.Name + BankAssetExtension;
                    string targetPathFull = bankTargetFolder + "/" + targetPathRelative;

                    EnsureFoldersExist(targetPathRelative, bankTargetFolder);

                    FileInfo targetInfo = new FileInfo(targetPathFull);

                    string stubData = RuntimeManager.BankStubPrefix + bankRef.Name;

                    // Minimise asset database refreshing by only writing the stub if necessary
                    bool writeStub;

                    if (targetInfo.Exists && targetInfo.Length == stubData.Length)
                    {
                        using (StreamReader reader = targetInfo.OpenText())
                        {
                            string contents = reader.ReadToEnd();
                            writeStub = (contents != stubData);
                        }
                    }
                    else
                    {
                        writeStub = true;
                    }

                    if (writeStub)
                    {
                        // Create or update the stub
                        using (StreamWriter writer = targetInfo.CreateText())
                        {
                            writer.Write(stubData);
                        }

                        madeChanges = true;

                        if (!targetInfo.Exists)
                        {
                            string assetPath = targetPathFull.Replace(Application.dataPath, "Assets");
                            AssetDatabase.ImportAsset(assetPath);

                            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                            AssetDatabase.SetLabels(obj, new string[] { FMODLabel });
                        }
                    }
                }

                RemoveEmptyFMODFolders(bankTargetFolder);
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("FMOD: Updating bank stubs in {0} to match {1}",
                    bankTargetFolder, bankSourceFolder);
                Debug.LogException(exception);
                return;
            }

            if (madeChanges)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.LogFormat("FMOD: Updated bank stubs in {0} to match {1}", bankTargetFolder, bankSourceFolder);
            }
        }

        private static void EnsureFoldersExist(string filePath, string basePath)
        {
            string dataPath = Application.dataPath + "/";

            if (!basePath.StartsWith(dataPath))
            {
                throw new ArgumentException(
                    string.Format("Base path {0} is not within the Assets folder", basePath), "basePath");
            }

            int lastSlash = filePath.LastIndexOf('/');

            if (lastSlash == -1)
            {
                // No folders
                return;
            }

            string assetString = filePath.Substring(0, lastSlash);

            string[] folders = assetString.Split('/');
            string parentFolder = "Assets/" + basePath.Substring(dataPath.Length);

            for (int i = 0; i < folders.Length; ++i)
            {
                string folderPath = parentFolder + "/" + folders[i];

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.CreateFolder(parentFolder, folders[i]);

                    var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
                    AssetDatabase.SetLabels(folder, new string[] { FMODLabel });
                }

                parentFolder = folderPath;
            }
        }

        private static void BuildTargetChanged()
        {
            RefreshBanks();
            #if UNITY_ANDROID
            Settings.Instance.AndroidUseOBB = PlayerSettings.Android.useAPKExpansionFiles;
            #endif
        }

        static void OnCacheChange()
        {
            List<string> masterBanks = new List<string>();
            List<string> banks = new List<string>();

            var settings = Settings.Instance;
            bool hasChanged = false;

            foreach (EditorBankRef bankRef in eventCache.MasterBanks)
            {
                masterBanks.Add(bankRef.Name);
            }

            if (!CompareLists(masterBanks, settings.MasterBanks))
            {
                settings.MasterBanks.Clear();
                settings.MasterBanks.AddRange(masterBanks);
                hasChanged = true;
            }

            foreach (var bankRef in eventCache.EditorBanks)
            {
                if (!eventCache.MasterBanks.Contains(bankRef) &&
                    !eventCache.StringsBanks.Contains(bankRef))
                {
                    banks.Add(bankRef.Name);
                }
            }
            banks.Sort((a, b) => string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase));

            if (!CompareLists(banks, settings.Banks))
            {
                settings.Banks.Clear();
                settings.Banks.AddRange(banks);
                hasChanged = true;
            }

            if (hasChanged)
            {
                EditorUtility.SetDirty(settings);
            }
        }

        public static DateTime CacheTime
        {
            get
            {
                if (eventCache != null)
                {
                    return eventCache.CacheTime;
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }

        public static List<EditorEventRef> Events
        {
            get
            {
                AffirmEventCache();
                return eventCache.EditorEvents;
            }
        }

        public static List<EditorBankRef> Banks
        {
            get
            {
                AffirmEventCache();
                return eventCache.EditorBanks;
            }
        }

        public static List<EditorParamRef> Parameters
        {
            get
            {
                AffirmEventCache();
                return eventCache.EditorParameters;
            }
        }

        public static List<EditorBankRef> MasterBanks
        { 
            get
            {
                AffirmEventCache();
                return eventCache.MasterBanks;
            }
        }

        public static bool IsLoaded
        {
            get
            {
                return Settings.Instance.SourceBankPath != null;
            }
        }

        public static bool IsValid
        {
            get
            {
                AffirmEventCache();
                return eventCache.CacheTime != DateTime.MinValue;
            }
        }

        public static bool IsInitialized
        {
            get
            {
                return eventCache != null;
            }
        }

        public static EditorEventRef EventFromPath(string pathOrGuid)
        {
            EditorEventRef eventRef;
            if (pathOrGuid.StartsWith("{"))
            {
                Guid guid = new Guid(pathOrGuid);
                eventRef = EventFromGUID(guid);
            }
            else
            {
                eventRef = EventFromString(pathOrGuid);
            }
            return eventRef;
        }

        public static EditorEventRef EventFromString(string path)
        {
            AffirmEventCache();
            return eventCache.EditorEvents.Find((x) => x.Path.Equals(path, StringComparison.CurrentCultureIgnoreCase));
        }

        public static EditorEventRef EventFromGUID(Guid guid)
        {
            AffirmEventCache();
            return eventCache.EditorEvents.Find((x) => x.Guid == guid);
        }

        public static EditorParamRef ParamFromPath(string name)
        {
            AffirmEventCache();
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
        public class PreprocessScene : IProcessSceneWithReport
        {
            public int callbackOrder { get { return 0; } }

            public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
            {
                if (report == null) return;

                CheckValidEventRefs(scene);
            }
        }
#else
        public class PreprocessScene : IProcessScene
        {
            public int callbackOrder { get { return 0; } }

            public void OnProcessScene(UnityEngine.SceneManagement.Scene scene)
            {
                CheckValidEventRefs(scene);
            }
        }
#endif

        private static bool CompareLists(List<string> tempBanks, List<string> banks)
        {
            if (tempBanks.Count != banks.Count)
                return false;

            for (int i = 0; i < tempBanks.Count; i++)
            {
                if (tempBanks[i] != banks[i])
                    return false;
            }
            return true;
        }

        private static bool AssetHasLabel(string assetPath, string label)
        {
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            string[] labels = AssetDatabase.GetLabels(asset);

            return labels.Contains(label);
        }

        const string AssetsFolderName = "Assets";

        public static void RemoveBanks(string basePath)
        {
            if (!Directory.Exists(basePath))
            {
                return;
            }

            string[] filePaths = Directory.GetFiles(basePath, "*", SearchOption.AllDirectories);

            foreach (string filePath in filePaths)
            {
                if (!filePath.EndsWith(".meta"))
                {
                    string assetPath = filePath.Replace(Application.dataPath, AssetsFolderName);

                    if (AssetHasLabel(assetPath, FMODLabel))
                    {
                        AssetDatabase.MoveAssetToTrash(assetPath);
                    }
                }
            }

            RemoveEmptyFMODFolders(basePath);

            if (Directory.GetFileSystemEntries(basePath).Length == 0)
            {
                string baseFolder = basePath.Replace(Application.dataPath, AssetsFolderName);
                AssetDatabase.MoveAssetToTrash(baseFolder);
            }
        }

        public static void MoveBanks(string from, string to)
        {
            if (!Directory.Exists(from))
            {
                return;
            }
            
            if (!Directory.Exists(to))
            {
                Directory.CreateDirectory(to);
            }

            string[] oldBankFiles = Directory.GetFiles(from);

            foreach (var oldBankFileName in oldBankFiles)
            {
                if (oldBankFileName.EndsWith(".meta"))
                    continue;
                string assetString = oldBankFileName.Replace(Application.dataPath, "Assets");
                AssetDatabase.ImportAsset(assetString);
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetString);
                string[] labels = AssetDatabase.GetLabels(obj);
                foreach (string label in labels)
                {
                    if (label.Equals("FMOD"))
                    {
                        AssetDatabase.MoveAsset(assetString, to);
                        break;
                    }
                }
            }
            if (Directory.GetFiles(Path.GetDirectoryName(oldBankFiles[0])).Length == 0)
            {
                Directory.Delete(Path.GetDirectoryName(oldBankFiles[0]));
            }
        }

        public static void RemoveEmptyFMODFolders(string basePath)
        {
            string[] folderPaths = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);

            // Process longest paths first so parent folders are cleared out when we get to them
            Array.Sort(folderPaths, (a, b) => b.Length.CompareTo(a.Length));

            foreach (string folderPath in folderPaths)
            {
                string assetPath = folderPath.Replace(Application.dataPath, AssetsFolderName);

                if (AssetHasLabel(assetPath, FMODLabel) && Directory.GetFileSystemEntries(folderPath).Length == 0)
                {
                    AssetDatabase.MoveAssetToTrash(assetPath);
                }
            }
        }
    }
}
