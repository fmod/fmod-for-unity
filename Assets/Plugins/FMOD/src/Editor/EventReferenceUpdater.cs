using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMODUnity
{
    public class EventReferenceUpdater : EditorWindow
    {
        public const string MenuPath = "FMOD/Update Event References";

        const string SearchButtonText = "Scan";

        const int EventReferenceTransitionVersion = 0x00020200;

        const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly string HelpText =
            string.Format("Click {0} to search your project for obsolete event references.", SearchButtonText);

        private readonly string[] SearchFolders = {
            "Assets",
        };

        private SceneSetup[] sceneSetup;

        private IEnumerator<string> processingState;

        private SearchProgress prefabProgress;
        private SearchProgress sceneProgress;
        private SearchProgress scriptableObjectProgress;

        [SerializeField]
        private List<Asset> assets = new List<Asset>();

        [SerializeField]
        private List<Component> components = new List<Component>();

        [SerializeField]
        private List<Task> tasks = new List<Task>();

        private int executableTaskCount = 0;

        private TreeViewState taskViewState = new TreeViewState();

        private TaskView taskView;

        [NonSerialized]
        private GUIContent status = GUIContent.none;

        [NonSerialized]
        private Task selectedTask;

        [NonSerialized]
        private Vector2 manualDescriptionScrollPosition;

        [NonSerialized]
        private static GUIContent AssetContent = new GUIContent("Asset");
        private static GUIContent ComponentTypeContent = new GUIContent("Component Type");
        private static GUIContent GameObjectContent = new GUIContent("Game Object");

        string ExecuteButtonText()
        {
            return string.Format("Execute {0} Selected Tasks", executableTaskCount);
        }

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            EventReferenceUpdater updater = GetWindow<EventReferenceUpdater>("FMOD Event Reference Updater");
            updater.minSize = new Vector2(800, 600);

            updater.SetStatus(HelpText);

            updater.Show();
        }

        public static bool IsUpToDate()
        {
            return Settings.Instance.LastEventReferenceScanVersion >= EventReferenceTransitionVersion;
        }

        private void BeginSearching()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                tasks.Clear();
                executableTaskCount = 0;
                taskView.SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                taskView.Reload();

                processingState = SearchProject();
            }
        }

        private void StopProcessing(bool isComplete)
        {
            processingState = null;

            if (isComplete)
            {
                if (tasks.Count == 0)
                {
                    SetStatus("No required tasks found. Event references are up to date.");
                    Settings.Instance.LastEventReferenceScanVersion = FMOD.VERSION.number;
                    EditorUtility.SetDirty(Settings.Instance);

                    SetupWizardWindow.SetUpdateTaskComplete(SetupWizardWindow.UpdateTaskType.UpdateEventReferences);
                }
                else if (tasks.All(x => x.HasExecuted))
                {
                    SetStatus("Finished executing tasks. New tasks may now be required. Please re-scan your project.");
                }
                else
                {
                    SetStatus("Finished scanning. Please execute the tasks above.");
                }
            }
            else
            {
                SetStatus("Cancelled.");
            }
        }

        private void BeginExecuting()
        {
            Task[] enabledTasks = tasks.Where(t => t.CanExecute()).ToArray();

            if (enabledTasks.Length == 0)
            {
                return;
            }

            Asset[] affectedAssets = enabledTasks.Select(t => assets[t.AssetIndex]).Distinct().ToArray();

            int prefabCount = affectedAssets.Count(a => IsPrefab(a.Type));
            int sceneCount = affectedAssets.Count(a => a.Type == AssetType.Scene);

            string warningText = string.Format(
                "Executing these {0} tasks will change {1} prefabs and {2} scenes on disk.\n\n" +
                "Please ensure you have committed any outstanding changes to source control before continuing!",
                enabledTasks.Length, prefabCount, sceneCount);

            if (!EditorUtility.DisplayDialog("Confirm Bulk Changes", warningText, ExecuteButtonText(), "Cancel"))
            {
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                processingState = ExecuteTasks(enabledTasks);
            }
        }

        private void Cancel()
        {
            if (IsProcessing)
            {
                StopProcessing(false);
            }
            else
            {
                Close();
            }
        }

        private bool IsProcessing { get { return processingState != null; } }

        private struct SearchProgress
        {
            private int maximum;
            private int current;

            public float Fraction()
            {
                return (maximum > 0) ? (current / (float)maximum) : 1;
            }

            public void Increment()
            {
                if (current < maximum)
                {
                    ++current;
                }
            }

            public SearchProgress(int total)
            {
                this.maximum = total;
                this.current = 0;
            }
        }

        private IEnumerator<string> SearchProject()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:GameObject", SearchFolders);
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", SearchFolders);
            string[] scriptableObjectGuids =
                AssetDatabase.FindAssets("t:ScriptableObject", SearchFolders).Distinct().ToArray();

            prefabProgress = new SearchProgress(prefabGuids.Length);
            sceneProgress = new SearchProgress(sceneGuids.Length);
            scriptableObjectProgress = new SearchProgress(scriptableObjectGuids.Length);

            return SearchPrefabs(prefabGuids)
                .Concat(SearchScriptableObjects(scriptableObjectGuids))
                .Concat(SearchScenes(sceneGuids))
                .GetEnumerator();
        }

        private IEnumerable<string> SearchPrefabs(string[] guids)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                yield return string.Format("Searching {0}", path);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                int assetIndex = -1;

                foreach (Task task in SearchGameObject(prefab, prefab))
                {
                    if (assetIndex < 0)
                    {
                        assetIndex = AddAsset(GetAssetType(prefab), path);
                    }

                    task.AssetIndex = assetIndex;

                    AddTask(task);
                }

                prefabProgress.Increment();
            }
        }

        private IEnumerable<string> SearchScriptableObjects(string[] guids)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                yield return string.Format("Searching {0}", path);

                IEnumerable<ScriptableObject> scriptableObjects =
                    AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>();

                int assetIndex = -1;

                foreach (ScriptableObject scriptableObject in scriptableObjects)
                {
                    int componentIndex = -1;

                    foreach (Task task in GetUpdateTasks(scriptableObject))
                    {
                        if (assetIndex < 0)
                        {
                            assetIndex = AddAsset(AssetType.ScriptableObject, path);
                        }

                        if (componentIndex < 0)
                        {
                            componentIndex = AddComponent(scriptableObject);
                        }

                        task.AssetIndex = assetIndex;
                        task.ComponentIndex = componentIndex;

                        AddTask(task);
                    }
                }

                scriptableObjectProgress.Increment();
            }
        }

        private IEnumerable<string> SearchScenes(string[] guids)
        {
            sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                yield return string.Format("Searching {0}", path);

                Scene scene = SceneManager.GetSceneByPath(path);

                if (!scene.IsValid())
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                }

                int assetIndex = -1;

                foreach (GameObject gameObject in scene.GetRootGameObjects())
                {
                    foreach (Task task in SearchGameObject(gameObject, null))
                    {
                        if (assetIndex < 0)
                        {
                            assetIndex = AddAsset(AssetType.Scene,  path);
                        }

                        task.AssetIndex = assetIndex;

                        AddTask(task);
                    }
                }

                sceneProgress.Increment();
            }

            if (sceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }
        }

        private IEnumerable<Task> SearchGameObject(GameObject gameObject, GameObject root)
        {
            MonoBehaviour[] behaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                int componentIndex = -1;

                foreach (Task task in GetUpdateTasks(behaviour))
                {
                    if (componentIndex < 0)
                    {
                        componentIndex = AddComponent(behaviour, root);
                    }

                    task.ComponentIndex = componentIndex;

                    yield return task;
                }
            }
        }

        static IEnumerable<Task> GetUpdateTasks(UnityEngine.Object target)
        {
            if (target == null)
            {
                return Enumerable.Empty<Task>();
            }
            else if (target is StudioEventEmitter)
            {
                return GetEmitterUpdateTasks(target as StudioEventEmitter);
            }
#if UNITY_TIMELINE_EXIST
            else if (target is FMODEventPlayable)
            {
                return GetPlayableUpdateTasks(target as FMODEventPlayable);
            }
#endif
            else
            {
                return GetGenericUpdateTasks(target);
            }
        }

        static IEnumerable<Task> GetEmitterUpdateTasks(StudioEventEmitter emitter)
        {
            bool hasOwnEvent = true;
            bool hasOwnEventReference = true;

            if (PrefabUtility.IsPartOfPrefabInstance(emitter))
            {
                StudioEventEmitter sourceEmitter = PrefabUtility.GetCorrespondingObjectFromSource(emitter);
                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(emitter);

                if (modifications != null) // GetPropertyModifications returns null if the prefab instance is disconnected
                {
                    hasOwnEvent = modifications.Any(
                        m => m.target == sourceEmitter && m.propertyPath == "Event");

                    hasOwnEventReference = modifications.Any(
                        m => m.target == sourceEmitter && m.propertyPath.StartsWith("EventReference"));
                }
            }

            if (hasOwnEventReference)
            {
                Task updateTask = GetUpdateEventReferenceTask(emitter.EventReference, "EventReference");
                if (updateTask != null)
                {
                    yield return updateTask;
                }

                if (hasOwnEvent)
                {
#pragma warning disable 0618 // Suppress warnings about using the obsolete StudioEventEmitter.Event field
                    if (!string.IsNullOrEmpty(emitter.Event))
#pragma warning restore 0618
                    {
                        if (emitter.EventReference.IsNull)
                        {
                            yield return Task.MoveEventToEventReference(emitter);
                        }
                        else
                        {
                            yield return Task.ClearEvent(emitter);
                        }
                    }
                }
            }
            else if (hasOwnEvent)
            {
                yield return Task.MoveEventOverrideToEventReference(emitter);
            }
        }

        static Task GetUpdateEventReferenceTask(EventReference eventReference, string fieldName)
        {
            if (eventReference.IsNull)
            {
                return null;
            }

            if (Settings.Instance.EventLinkage == EventLinkage.GUID)
            {
                EditorEventRef editorEventRef = EventManager.EventFromGUID(eventReference.Guid);

                if (editorEventRef == null)
                {
                    return null;
                }
                
                if (eventReference.Path != editorEventRef.Path)
                {
                    return Task.UpdateEventReferencePath(fieldName, eventReference.Path, editorEventRef.Path,
                        eventReference.Guid);
                }
            }
            else if (Settings.Instance.EventLinkage == EventLinkage.Path)
            {
                EditorEventRef editorEventRef = EventManager.EventFromPath(eventReference.Path);

                if (editorEventRef != null)
                {
                    if (eventReference.Guid != editorEventRef.Guid)
                    {
                        return Task.UpdateEventReferenceGuid(fieldName, eventReference.Guid, editorEventRef.Guid,
                            eventReference.Path);
                    }
                }
                else if (!eventReference.Guid.IsNull)
                {
                    editorEventRef = EventManager.EventFromGUID(eventReference.Guid);

                    if (editorEventRef != null)
                    {
                        return Task.UpdateEventReferencePath(fieldName, eventReference.Path, editorEventRef.Path,
                            eventReference.Guid);
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unrecognized EventLinkage: " + Settings.Instance.EventLinkage);
            }

            return null;
        }

#if UNITY_TIMELINE_EXIST
        static IEnumerable<Task> GetPlayableUpdateTasks(FMODEventPlayable playable)
        {
            Task updateTask = GetUpdateEventReferenceTask(playable.eventReference, "eventReference");
            if (updateTask != null)
            {
                yield return updateTask;
            }

#pragma warning disable 0618 // Suppress warnings about using the obsolete FMODEventPlayable.eventName field
            if (!string.IsNullOrEmpty(playable.eventName))
#pragma warning restore 0618
            {
                if (playable.eventReference.IsNull)
                {
                    yield return Task.MoveEventNameToEventReference(playable);
                }
                else
                {
                    yield return Task.ClearEventName(playable);
                }
            }
        }
#endif

#pragma warning disable 0618 // Suppress a warning about using the obsolete EventRefAttribute class
        static bool IsEventRef(FieldInfo field)
        {
            return field.FieldType == typeof(string) && EditorUtils.HasAttribute<EventRefAttribute>(field);
        }
#pragma warning restore 0618

        static T GetCustomAttribute<T>(FieldInfo field)
            where T : Attribute
        {
            return Attribute.GetCustomAttribute(field, typeof(T)) as T;
        }

        static IEnumerable<Task> GetGenericUpdateTasks(UnityEngine.Object target)
        {
            FieldInfo[] fields = target.GetType().GetFields(DefaultBindingFlags);

            List<FieldInfo> oldFields = fields.Where(IsEventRef).ToList();

            int initialOldFieldCount = oldFields.Count;

            List<FieldInfo> newFields = fields.Where(f => f.FieldType == typeof(EventReference)).ToList();

            // Remove empty [EventRef] fields
            for (int i = 0; i < oldFields.Count; )
            {
                FieldInfo oldField = oldFields[i];

                if (string.IsNullOrEmpty(oldField.GetValue(target) as string))
                {
                    oldFields.RemoveAt(i);

                    yield return Task.RemoveEmptyEventRefField(oldField.Name);
                }
                else
                {
                    ++i;
                }
            }

            // Handle conflicts where multiple [EventRef] fields have the same migration target
#pragma warning disable 0618 // Suppress a warning about using the obsolete EventRefAttribute class
            IGrouping<string, FieldInfo>[] conflictingGroups = oldFields
                .GroupBy(f => GetCustomAttribute<EventRefAttribute>(f).MigrateTo)
                .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
                .ToArray();
#pragma warning restore 0618
            
            foreach (IGrouping<string, FieldInfo> group in conflictingGroups)
            {
                foreach (FieldInfo field in group)
                {
                    oldFields.Remove(field);
                }

                yield return Task.FixMigrationTargetConflict(group.Select(f => f.Name).ToArray());
            }

            // Handle [EventRef] fields with MigrateTo set
#pragma warning disable 0618 // Suppress a warning about using the obsolete EventRefAttribute class
            for (int i = 0; i < oldFields.Count; )
            {
                FieldInfo oldField = oldFields[i];

                EventRefAttribute attribute = GetCustomAttribute<EventRefAttribute>(oldField);

                if (!string.IsNullOrEmpty(attribute.MigrateTo))
                {
                    oldFields.RemoveAt(i);

                    string oldValue = oldField.GetValue(target) as string;

                    FieldInfo newField = newFields.FirstOrDefault(f => f.Name == attribute.MigrateTo);

                    if (newField != null)
                    {
                        EventReference newValue = (EventReference)newField.GetValue(target);

                        if (newValue.IsNull)
                        {
                            yield return Task.MoveEventRefFieldToEventReferenceField(
                                oldValue, oldField.Name, newField.Name);
                        }
                        else
                        {
                            yield return Task.RemoveEventRefField(oldValue, oldField.Name);
                        }
                    }
                    else
                    {
                        yield return Task.AddMigrationTarget(oldValue, oldField.Name, attribute.MigrateTo);
                    }
                }
                else
                {
                    ++i;
                }
            }
#pragma warning restore 0618

            // Auto-migrate if there is a single old field that hasn't been handled already,
            // and there is a single new field
            if (initialOldFieldCount == 1 && oldFields.Count == 1 && newFields.Count == 1)
            {
                FieldInfo oldField = oldFields[0];

                string oldValue = oldField.GetValue(target) as string;

                FieldInfo newField = newFields[0];

                EventReference newValue = (EventReference)newField.GetValue(target);

                if (newValue.IsNull)
                {
                    yield return Task.MoveEventRefFieldToEventReferenceField(
                        oldValue, oldField.Name, newField.Name);
                }
                else
                {
                    yield return Task.RemoveEventRefField(oldValue, oldField.Name);
                }

                oldFields.RemoveAt(0);
            }

            // Handle old fields with no migration target
            foreach (FieldInfo oldField in oldFields)
            {
                yield return Task.AddMigrationTarget(oldField.GetValue(target) as string, oldField.Name);
            }

            // Check new fields for GUID/path mismatches
            foreach (FieldInfo newField in newFields)
            {
                EventReference eventReference = (EventReference)newField.GetValue(target);

                Task updateTask = GetUpdateEventReferenceTask(eventReference, newField.Name);
                if (updateTask != null)
                {
                    yield return updateTask;
                }
            }
        }

        private IEnumerator<string> ExecuteTasks(Task[] tasks)
        {
            sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            foreach (Task task in tasks)
            {
                yield return string.Format("Executing: {0}", task);

                ExecuteTask(task, SavePolicy.AutoSave);
            }

            EditorSceneManager.SaveOpenScenes();
            UpdateExecutableTaskCount();

            if (sceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }
        }

        private enum AssetType
        {
            Scene,
            Prefab,
            PrefabModel,
            PrefabVariant,
            ScriptableObject,
        }

        private static bool IsPrefab(AssetType type)
        {
            return type == AssetType.Prefab
                || type == AssetType.PrefabModel
                || type == AssetType.PrefabVariant;
        }

        private static AssetType GetAssetType(GameObject gameObject)
        {
            PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(gameObject);

            if (prefabType == PrefabAssetType.Model)
            {
                return AssetType.PrefabModel;
            }
            else if (prefabType == PrefabAssetType.Variant)
            {
                return AssetType.PrefabVariant;
            }
            else
            {
                return AssetType.Prefab;
            }
        }

        private enum EnableState
        {
            Enabled,
            Disabled,
            Mixed,
        }

        [Serializable]
        private class Asset
        {
            public AssetType Type;
            public string Path;
            public EnableState EnableState;
        }

        [Serializable]
        private class Component
        {
            public GlobalObjectId GameObjectID;
            public string Type;
            public string Path;
            public string ScriptPath;
        }

        [Serializable]
        private class Task
        {
            public bool Enabled = true;
            public int AssetIndex; // index into the assets list
            public int ComponentIndex; // index into the components list

            private Type type;
            private string[] Data;

            const string EmitterEventField = "Event";
            const string EmitterEventReferenceField = "EventReference";
            const string PlayableEventNameField = "eventName";
            const string PlayableEventReferenceField = "eventReference";

            private delegate string DescriptionDelegate(string[] data);
            private delegate string ManualInstructionsDelegate(string[] data, Component component);
            private delegate bool IsValidDelegate(string[] data, UnityEngine.Object target);
            private delegate void ExecuteDelegate(string[] data, UnityEngine.Object target);

            private static readonly Delegates[] Implementations;

            private enum Type
            {
                EmitterClearEvent,
                EmitterMoveEventToEventReference,
                EmitterMoveEventOverrideToEventReference,
                PlayableClearEventName,
                PlayableMoveEventNameToEventReference,
                GenericRemoveEventRefField,
                GenericRemoveEmptyEventRefField,
                GenericMoveEventRefFieldToEventReferenceField,
                GenericAddMigrationTarget,
                GenericUpdateEventReferencePath,
                GenericUpdateEventReferenceGuid,
                GenericFixMigrationTargetConflict,

                Count
            }

            public bool HasExecuted { get; private set; }

            // Suppress warnings about using the obsolete StudioEventEmitter.Event and FMODEventPlayable.eventName fields
#pragma warning disable 0618
            public static Task ClearEvent(StudioEventEmitter emitter)
            {
                return new Task()
                {
                    type = Type.EmitterClearEvent,
                    Data = new string[] { emitter.Event },
                };
            }

#if UNITY_TIMELINE_EXIST
            public static Task ClearEventName(FMODEventPlayable playable)
            {
                return new Task()
                {
                    type = Type.PlayableClearEventName,
                    Data = new string[] { playable.eventName },
                };
            }
#endif

            public static Task MoveEventToEventReference(StudioEventEmitter emitter)
            {
                return new Task()
                {
                    type = Type.EmitterMoveEventToEventReference,
                    Data = new string[] { emitter.Event },
                };
            }


#if UNITY_TIMELINE_EXIST
            public static Task MoveEventNameToEventReference(FMODEventPlayable playable)
            {
                return new Task()
                {
                    type = Type.PlayableMoveEventNameToEventReference,
                    Data = new string[] { playable.eventName },
                };
            }
#endif

            public static Task MoveEventOverrideToEventReference(StudioEventEmitter emitter)
            {
                return new Task()
                {
                    type = Type.EmitterMoveEventOverrideToEventReference,
                    Data = new string[] { emitter.Event },
                };
            }
#pragma warning restore 0618

            public static Task RemoveEventRefField(string value, string fieldName)
            {
                return new Task()
                {
                    type = Type.GenericRemoveEventRefField,
                    Data = new string[] { value, fieldName },
                };
            }

            public static Task RemoveEmptyEventRefField(string fieldName)
            {
                return new Task()
                {
                    type = Type.GenericRemoveEmptyEventRefField,
                    Data = new string[] { fieldName },
                };
            }

            public static Task MoveEventRefFieldToEventReferenceField(
                string value, string oldFieldName, string newFieldName)
            {
                return new Task()
                {
                    type = Type.GenericMoveEventRefFieldToEventReferenceField,
                    Data = new string[] { value, oldFieldName, newFieldName },
                };
            }

            public static Task AddMigrationTarget(string value, string fieldName, string targetName = null)
            {
                return new Task()
                {
                    type = Type.GenericAddMigrationTarget,
                    Data = new string[] { value, fieldName, targetName },
                };
            }

            public static Task UpdateEventReferencePath(string fieldName, string oldPath, string newPath,
                FMOD.GUID guid)
            {
                return new Task()
                {
                    type = Type.GenericUpdateEventReferencePath,
                    Data = new string[] { fieldName, oldPath, newPath, guid.ToString() },
                };
            }

            public static Task UpdateEventReferenceGuid(string fieldName, FMOD.GUID oldGuid, FMOD.GUID newGuid,
                string path)
            {
                return new Task()
                {
                    type = Type.GenericUpdateEventReferenceGuid,
                    Data = new string[] { fieldName, oldGuid.ToString(), newGuid.ToString(), path },
                };
            }

            public static Task FixMigrationTargetConflict(params string[] fieldNames)
            {
                return new Task()
                {
                    type = Type.GenericFixMigrationTargetConflict,
                    Data = fieldNames,
                };
            }

            private struct Delegates
            {
                public DescriptionDelegate Description;
                public ManualInstructionsDelegate ManualInstructions;
                public IsValidDelegate IsValid;
                public ExecuteDelegate Execute;
            }

            private static void Implement(Type type,
                DescriptionDelegate Description,
                IsValidDelegate IsValid,
                ExecuteDelegate Execute,
                ManualInstructionsDelegate ManualInstructions = null)
            {
                Implementations[(int)type] = new Delegates() {
                    Description = Description,
                    IsValid = IsValid,
                    Execute = Execute,
                    ManualInstructions = ManualInstructions,
                };
            }

            private Delegates GetDelegates()
            {
                return Implementations[(int)type];
            }

            static Task()
            {
                Implementations = new Delegates[(int)Type.Count];

                // Suppress warnings about using the obsolete StudioEventEmitter.Event
                // and FMODEventPlayable.eventName fields
#pragma warning disable 0618

                Implement(Type.EmitterClearEvent,
                    Description: (data) => {
                        return string.Format("Clear <b>'{0}'</b> from the <b>{1}</b> field", data[0], EmitterEventField);
                    },
                    IsValid: (data, target) => {
                        StudioEventEmitter emitter = target as StudioEventEmitter;
                        return emitter != null && emitter.Event == data[0] && !emitter.EventReference.IsNull;
                    },
                    Execute: (data, target) => {
                        StudioEventEmitter emitter = target as StudioEventEmitter;

                        emitter.Event = string.Empty;
                        EditorUtility.SetDirty(emitter);
                    }
                );
                Implement(Type.EmitterMoveEventToEventReference,
                    Description: (data) => {
                        return string.Format("Move <b>'{0}'</b> from <b>{1}</b> to <b>{2}</b>",
                            data[0], EmitterEventField, EmitterEventReferenceField);
                    },
                    IsValid: (data, target) => {
                        StudioEventEmitter emitter = target as StudioEventEmitter;
                        return emitter != null && emitter.Event == data[0] && emitter.EventReference.IsNull;
                    },
                    Execute: (data, target) => {
                        StudioEventEmitter emitter = target as StudioEventEmitter;

                        emitter.EventReference.Path = emitter.Event;
                        emitter.Event = string.Empty;

                        EditorEventRef eventRef = EventManager.EventFromPath(emitter.EventReference.Path);

                        if (eventRef != null)
                        {
                            emitter.EventReference.Guid = eventRef.Guid;
                        }

                        EditorUtility.SetDirty(emitter);
                    }
                );
                Implement(Type.EmitterMoveEventOverrideToEventReference,
                    Description: (data) => {
                        return string.Format("Move prefab override <b>'{0}'</b> from <b>{1}</b> to <b>{2}</b>",
                            data[0], EmitterEventField, EmitterEventReferenceField);
                    },
                    IsValid: (data, target) => {
                        if (!PrefabUtility.IsPartOfPrefabInstance(target))
                        {
                            return false;
                        }

                        StudioEventEmitter emitter = target as StudioEventEmitter;

                        if (emitter == null)
                        {
                            return false;
                        }

                        StudioEventEmitter sourceEmitter = PrefabUtility.GetCorrespondingObjectFromSource(emitter);

                        if (sourceEmitter == null)
                        {
                            return false;
                        }

                        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(emitter);
                        PropertyModification eventOverride = modifications.FirstOrDefault(
                            m => m.target == sourceEmitter && m.propertyPath == "Event");

                        if (eventOverride == null || eventOverride.value != data[0])
                        {
                            return false;
                        }

                        bool hasEventReferenceOverride = modifications.Any(
                            m => m.target == sourceEmitter && m.propertyPath.StartsWith("EventReference"));

                        if (hasEventReferenceOverride)
                        {
                            return false;
                        }

                        return true;
                    },
                    Execute: (data, target) => {
                        StudioEventEmitter emitter = target as StudioEventEmitter;

                        string path = emitter.Event;

                        // Clear the Event override
                        StudioEventEmitter sourceEmitter = PrefabUtility.GetCorrespondingObjectFromSource(emitter);
                        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(emitter);

                        modifications = modifications
                            .Where(m => !(m.target == sourceEmitter && m.propertyPath == "Event"))
                            .ToArray();

                        PrefabUtility.SetPropertyModifications(emitter, modifications);

                        // Set the EventReference override
                        emitter.EventReference.Path = path;

                        EditorEventRef eventRef = EventManager.EventFromPath(path);

                        if (eventRef != null)
                        {
                            emitter.EventReference.Guid = eventRef.Guid;
                        }

                        EditorUtility.SetDirty(emitter);
                    }
                );

#if UNITY_TIMELINE_EXIST
                Implement(Type.PlayableClearEventName,
                    Description: (data) => {
                        return string.Format("Clear <b>'{0}'</b> from the <b>{1}</b> field", data[0], PlayableEventNameField);
                    },
                    IsValid: (data, target) => {
                        FMODEventPlayable playable = target as FMODEventPlayable;
                        return playable != null && playable.eventName == data[0] && !playable.eventReference.IsNull;
                    },
                    Execute: (data, target) => {
                        FMODEventPlayable playable = target as FMODEventPlayable;

                        playable.eventName = string.Empty;
                        EditorUtility.SetDirty(playable);
                    }
                );
                Implement(Type.PlayableMoveEventNameToEventReference,
                    Description: (data) => {
                        return string.Format("Move <b>'{0}'</b> from <b>{1}</b> to <b>{2}</b>",
                            data[0], PlayableEventNameField, PlayableEventReferenceField);
                    },
                    IsValid: (data, target) => {
                        FMODEventPlayable playable = target as FMODEventPlayable;
                        return playable != null && playable.eventName == data[0] && playable.eventReference.IsNull;
                    },
                    Execute: (data, target) => {
                        FMODEventPlayable playable = target as FMODEventPlayable;

                        playable.eventReference.Path = playable.eventName;
                        playable.eventName = string.Empty;

                        EditorEventRef eventRef = EventManager.EventFromPath(playable.eventReference.Path);

                        if (eventRef != null)
                        {
                            playable.eventReference.Guid = eventRef.Guid;
                        }

                        EditorUtility.SetDirty(playable);
                    }
                );
#endif
                Implement(Type.GenericRemoveEventRefField,
                    Description: (data) => {
                        return string.Format("Remove field <b>{0}</b>", data[1]);
                    },
                    ManualInstructions: (data, component) => {
                        return string.Format(
                            "The {1} field on component {2} has value '{0}', " +
                            "but the corresponding EventReference field already has a value.\n" +
                            "* Ensure no other instances of {2} are using the {1} field\n" +
                            "* Edit {3} and remove the {1} field",
                            data[0], data[1], component.Type, component.ScriptPath);
                    },
                    IsValid: (data, target) => {
                        System.Type behaviourType = target.GetType();
                        FieldInfo field = behaviourType.GetField(data[1]);

                        return field != null && IsEventRef(field) && (field.GetValue(target) as string) == data[0];
                    },
                    Execute: null
                );
                Implement(Type.GenericRemoveEmptyEventRefField,
                    Description: (data) => {
                        return string.Format("Remove empty field <b>{0}</b>", data[0]);
                    },
                    ManualInstructions: (data, component) => {
                        return string.Format(
                            "The {0} field on component {1} is empty.\n" +
                            "* Ensure no other instances of {1} are using the {0} field\n" +
                            "* Edit {2} and remove the {0} field",
                            data[0], component.Type, component.ScriptPath);
                    },
                    IsValid: (data, target) => {
                        System.Type behaviourType = target.GetType();
                        FieldInfo field = behaviourType.GetField(data[0]);

                        return field != null && IsEventRef(field)
                            && string.IsNullOrEmpty(field.GetValue(target) as string);
                    },
                    Execute: null
                );
                Implement(Type.GenericMoveEventRefFieldToEventReferenceField,
                    Description: (data) => {
                        return string.Format("Move <b>'{0}'</b> from <b>{1}</b> to <b>{2}</b>",
                            data[0], data[1], data[2]);
                    },
                    IsValid: (data, target) => {
                        string value = data[0];
                        string oldFieldName = data[1];
                        string newFieldName = data[2];

                        System.Type behaviourType = target.GetType();

                        FieldInfo oldField = behaviourType.GetField(oldFieldName, DefaultBindingFlags);
                        FieldInfo newField = behaviourType.GetField(newFieldName, DefaultBindingFlags);

                        if (oldField == null || newField == null
                            || !IsEventRef(oldField)
                            || newField.FieldType != typeof(EventReference))
                        {
                            return false;
                        }

                        string oldValue = oldField.GetValue(target) as string;
                        EventReference newValue = (EventReference)newField.GetValue(target);

                        return oldValue == value && newValue.IsNull;
                    },
                    Execute: (data, target) => {
                        string path = data[0];
                        string oldFieldName = data[1];
                        string newFieldName = data[2];

                        System.Type type = target.GetType();

                        FieldInfo oldField = type.GetField(oldFieldName, DefaultBindingFlags);
                        FieldInfo newField = type.GetField(newFieldName, DefaultBindingFlags);

                        EventReference eventReference = new EventReference() { Path = path };

                        EditorEventRef eventRef = EventManager.EventFromPath(path);

                        if (eventRef != null)
                        {
                            eventReference.Guid = eventRef.Guid;
                        }

                        oldField.SetValue(target, string.Empty);
                        newField.SetValue(target, eventReference);

                        EditorUtility.SetDirty(target);
                    }
                );
                Implement(Type.GenericAddMigrationTarget,
                    Description: (data) => {
                        string targetName = data[2];

                        if (!string.IsNullOrEmpty(targetName))
                        {
                            return string.Format(
                                "Add an <b>EventReference</b> field named <b>{0}</b> to hold <b>'{1}'</b> from <b>{2}</b>",
                                targetName, data[0], data[1]);
                        }
                        else
                        {
                            return string.Format("Add an <b>EventReference</b> field to hold <b>'{0}'</b> from <b>{1}</b>",
                                data[0], data[1]);
                        }
                    },
                    ManualInstructions: (data, component) => {
                        string targetName = data[2];

                        if (!string.IsNullOrEmpty(targetName))
                        {
                            return string.Format(
                                "The {0} field on component {1} has an [EventRef(MigrateTo=\"{2}\")] " +
                                "attribute, but the {2} field doesn't exist.\n" +
                                "* Edit {3} and add an EventReference field named {2}:\n" +
                                "    public EventReference {2};\n" +
                                "* Re-scan your project",
                                data[1], component.Type, targetName, component.ScriptPath);
                        }
                        else
                        {
                            return string.Format(
                                "The {0} field on component {1} has an [EventRef] " +
                                "attribute with no migration target specified.\n" +
                                "* Edit {2} and add an EventReference field:\n" +
                                "    public EventReference <fieldname>;\n" +
                                "* Change the [EventRef] attribute on {0} to:\n" +
                                "    [EventRef(MigrateTo=\"<fieldname>\")]\n" +
                                "* Re-scan your project.",
                                data[1], component.Type, component.ScriptPath);
                        }
                    },
                    IsValid: (data, target) => {
                        string value = data[0];
                        string oldFieldName = data[1];

                        System.Type behaviourType = target.GetType();

                        FieldInfo oldField = behaviourType.GetField(oldFieldName, DefaultBindingFlags);

                        return oldField != null && IsEventRef(oldField)
                            && (oldField.GetValue(target) as string) == value;
                    },
                    Execute: null
                );
                Implement(Type.GenericUpdateEventReferencePath,
                    Description: (data) => {
                        return string.Format(
                            "Change the path on field <b>{0}</b> " +
                            "from <b>'{1}'</b> to <b>'{2}'</b> (to match GUID <b>{3}</b>)",
                            data[0], data[1], data[2], data[3]);
                    },
                    IsValid: (data, target) => {
                        System.Type targetType = target.GetType();
                        FieldInfo field = targetType.GetField(data[0], DefaultBindingFlags);

                        if (field == null || field.FieldType != typeof(EventReference))
                        {
                            return false;
                        }

                        EventReference value = (EventReference)field.GetValue(target);

                        return value.Path == data[1] && value.Guid.ToString() == data[3];
                    },
                    Execute: (data, target) => {
                        System.Type targetType = target.GetType();
                        FieldInfo field = targetType.GetField(data[0], DefaultBindingFlags);

                        EventReference value = (EventReference)field.GetValue(target);
                        value.Path = data[2];

                        field.SetValue(target, value);

                        EditorUtility.SetDirty(target);
                    }
                );
                Implement(Type.GenericUpdateEventReferenceGuid,
                    Description: (data) => {
                        return string.Format(
                            "Change the GUID on field <b>{0}</b> " +
                            "from <b>{1}</b> to <b>{2}</b> (to match path <b>'{3}'</b>)",
                            data[0], data[1], data[2], data[3]);
                    },
                    IsValid: (data, target) => {
                        System.Type targetType = target.GetType();
                        FieldInfo field = targetType.GetField(data[0], DefaultBindingFlags);

                        if (field == null || field.FieldType != typeof(EventReference))
                        {
                            return false;
                        }

                        EventReference value = (EventReference)field.GetValue(target);

                        return value.Guid.ToString() == data[1] && value.Path == data[3];
                    },
                    Execute: (data, target) => {
                        System.Type targetType = target.GetType();
                        FieldInfo field = targetType.GetField(data[0], DefaultBindingFlags);

                        EventReference value = (EventReference)field.GetValue(target);
                        value.Guid = FMOD.GUID.Parse(data[2]);

                        field.SetValue(target, value);

                        EditorUtility.SetDirty(target);
                    }
                );
                Implement(Type.GenericFixMigrationTargetConflict,
                    Description: (data) => {
                        return string.Format("Fix conflicting migration targets on fields <b>{0}</b>",
                            EditorUtils.SeriesString("</b>, <b>", "</b> and <b>", data));
                    },
                    ManualInstructions: (data, component) => {
                        return string.Format(
                            "Fields {0} on component {1} have [EventRef] attributes with the same MigrateTo value.\n" +
                            "* Edit {2} and make sure all [EventRef] attributes have different MigrateTo values\n" +
                            "* Re-scan your project",
                            EditorUtils.SeriesString(", ", " and ", data), component.Type, component.ScriptPath);
                    },
                    IsValid: (data, target) => {
                        return true;
                    },
                    Execute: null
                );

#pragma warning restore 0618
            }

            public override string ToString()
            {
                return GetDelegates().Description(Data);
            }

            public string PlainDescription()
            {
                return Regex.Replace(ToString(), "</?b>", string.Empty);
            }

            public string ManualInstructions(Component component)
            {
                Delegates delegates = GetDelegates();

                if (delegates.ManualInstructions != null)
                {
                    return delegates.ManualInstructions(Data, component);
                }
                else
                {
                    return null;
                }
            }

            public bool CanExecute()
            {
                return Enabled && !IsManual() && !HasExecuted;
            }

            public bool IsManual()
            {
                return GetDelegates().Execute == null;
            }

            public bool IsValid(UnityEngine.Object target)
            {
                return GetDelegates().IsValid(Data, target);
            }

            public bool Execute(UnityEngine.Object target)
            {
                if (IsValid(target))
                {
                    Delegates delegates = GetDelegates();

                    if (delegates.Execute != null)
                    {
                        delegates.Execute(Data, target);
                        HasExecuted = true;
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void ExecuteTask(Task task, SavePolicy savePolicy)
        {
            Asset asset = assets[task.AssetIndex];

            if (asset.Type == AssetType.ScriptableObject)
            {
                ExecuteScriptableObjectTask(task, savePolicy);
            }
            else
            {
                ExecuteGameObjectTask(task, savePolicy);
            }
        }

        private void ExecuteScriptableObjectTask(Task task, SavePolicy savePolicy)
        {
            Asset asset = assets[task.AssetIndex];
            Component component = components[task.ComponentIndex];

            IEnumerable<ScriptableObject> scriptableObjects =
                AssetDatabase.LoadAllAssetsAtPath(asset.Path).OfType<ScriptableObject>();

            foreach (ScriptableObject scriptableObject in scriptableObjects)
            {
                if (scriptableObject.GetType().Name == component.Type)
                {
                    if (task.Execute(scriptableObject))
                    {
                        break;
                    }
                }
            }
        }

        private void ExecuteGameObjectTask(Task task, SavePolicy savePolicy)
        {
            GameObject gameObject = LoadTargetGameObject(task, savePolicy);

            if (gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);

            Component component = components[task.ComponentIndex];

            foreach (MonoBehaviour behaviour in gameObject.GetComponents<MonoBehaviour>())
            {
                if (behaviour.GetType().Name == component.Type)
                {
                    if (task.Execute(behaviour))
                    {
                        break;
                    }
                }
            }
        }

        private enum SavePolicy
        {
            AskToSave,
            AutoSave,
        }

        private GameObject LoadTargetGameObject(Task task, SavePolicy savePolicy)
        {
            Asset asset = assets[task.AssetIndex];
            Component component = components[task.ComponentIndex];

            if (IsPrefab(asset.Type))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset.Path);

                if (prefab == null)
                {
                    return null;
                }

                if (!AssetDatabase.OpenAsset(prefab))
                {
                    return null;
                }

                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(component.GameObjectID) as GameObject;
            }
            else if (asset.Type == AssetType.Scene)
            {
                Scene scene = SceneManager.GetSceneByPath(asset.Path);

                if (!scene.IsValid())
                {
                    if (savePolicy == SavePolicy.AskToSave)
                    {
                        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            return null;
                        }
                    }
                    else if (savePolicy == SavePolicy.AutoSave)
                    {
                        EditorSceneManager.SaveOpenScenes();
                    }
                    else
                    {
                        throw new ArgumentException("Unrecognized SavePolicy: " + savePolicy, "savePolicy");
                    }

                    scene = EditorSceneManager.OpenScene(asset.Path, OpenSceneMode.Single);

                    if (!scene.IsValid())
                    {
                        return null;
                    }
                }

                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(component.GameObjectID) as GameObject;
            }
            else
            {
                return null;
            }
        }

        private int AddAsset(AssetType type, string path)
        {
            Asset asset = new Asset() {
                Type = type,
                Path = path,
            };

            assets.Add(asset);

            return assets.Count - 1;
        }

        private int AddComponent(MonoBehaviour behaviour, GameObject root)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);

            Component component = new Component() {
                GameObjectID = GlobalObjectId.GetGlobalObjectIdSlow(behaviour.gameObject),
                Type = behaviour.GetType().Name,
                Path = EditorUtils.GameObjectPath(behaviour, root),
                ScriptPath = AssetDatabase.GetAssetPath(script),
            };

            components.Add(component);

            return components.Count - 1;
        }

        private int AddComponent(ScriptableObject scriptableObject)
        {
            MonoScript script = MonoScript.FromScriptableObject(scriptableObject);

            Component component = new Component() {
                Type = scriptableObject.GetType().Name,
                ScriptPath = AssetDatabase.GetAssetPath(script),
            };

            components.Add(component);

            return components.Count - 1;
        }

        private void UpdateExecutableTaskCount()
        {
            executableTaskCount = tasks.Count(t => t.CanExecute());
        }

        private void AddTask(Task task)
        {
            tasks.Add(task);
            UpdateExecutableTaskCount();
            taskView.Reload();
            taskView.ExpandAll();
        }

        private void UpdateProcessing()
        {
            if (processingState != null)
            {
                if (processingState.MoveNext())
                {
                    SetStatus(processingState.Current);
                }
                else
                {
                    StopProcessing(true);
                }

                Repaint();
            }
        }

        void OnEnable()
        {
            taskView = new TaskView(taskViewState, tasks, assets, components);
            taskView.Reload();
            taskView.taskSelected += OnTaskSelected;
            taskView.taskDoubleClicked += OnTaskDoubleClicked;
            taskView.taskEnableStateChanged += OnTaskEnableStateChanged;
            taskView.assetEnableStateChanged += ApplyAssetEnableStateToTasks;

            EditorApplication.update += UpdateProcessing;
        }

        void OnDisable()
        {
            EditorApplication.update -= UpdateProcessing;
        }

        private void OnTaskSelected(Task task)
        {
            selectedTask = task;
        }

        private void OnTaskDoubleClicked(Task task)
        {
            Asset asset = assets[task.AssetIndex];

            if (asset.Type == AssetType.ScriptableObject)
            {
                UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset.Path);

                if (target == null)
                {
                    return;
                }

                if (!AssetDatabase.OpenAsset(target))
                {
                    return;
                }

                Component component = components[task.ComponentIndex];

                IEnumerable<ScriptableObject> scriptableObjects =
                    AssetDatabase.LoadAllAssetsAtPath(asset.Path).OfType<ScriptableObject>();

                foreach (ScriptableObject scriptableObject in scriptableObjects)
                {
                    if (scriptableObject.GetType().Name == component.Type
                        && task.IsValid(scriptableObject))
                    {
                        Selection.activeObject = scriptableObject;
                    }
                }
            }
            else
            {
                GameObject gameObject = LoadTargetGameObject(task, SavePolicy.AskToSave);

                if (gameObject == null)
                {
                    return;
                }

                Selection.activeGameObject = gameObject;
                EditorGUIUtility.PingObject(gameObject);
            }
        }

        private void OnTaskEnableStateChanged(Task task)
        {
            UpdateAssetEnableState(task.AssetIndex);
            UpdateExecutableTaskCount();
        }

        private void UpdateAssetEnableState(int assetIndex)
        {
            Asset asset = assets[assetIndex];

            asset.EnableState = tasks
                .Where(t => t.AssetIndex == assetIndex)
                .Select(t => t.Enabled ? EnableState.Enabled : EnableState.Disabled)
                .Aggregate((current, next) => (current == next) ? current : EnableState.Mixed);
        }

        private void ApplyAssetEnableStateToTasks(Asset asset)
        {
            int assetIndex = assets.IndexOf(asset);

            foreach (Task task in tasks.Where(t => t.AssetIndex == assetIndex))
            {
                task.Enabled = (asset.EnableState == EnableState.Enabled);
            }

            UpdateExecutableTaskCount();
        }

        private class Styles
        {
            public static GUIStyle RichText;
            public static GUIStyle RichTextBox;
            public static GUIStyle TreeViewRichText;

            private static bool Initialized = false;

            public static void Affirm()
            {
                if (!Initialized)
                {
                    Initialized = true;

                    RichText = new GUIStyle(GUI.skin.label) { richText = true };
                    RichTextBox = new GUIStyle(EditorStyles.helpBox) { richText = true };
                    TreeViewRichText = new GUIStyle(TreeView.DefaultStyles.label) { richText = true };
                }
            }
        }

        private class Icons
        {
            public static Texture2D Scene;
            public static Texture2D Prefab;
            public static Texture2D PrefabModel;
            public static Texture2D PrefabVariant;
            public static Texture2D ScriptableObject;
            public static Texture2D GameObject;

            private static bool Initialized = false;

            public static void Affirm()
            {
                if (!Initialized)
                {
                    Initialized = true;

                    Scene = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
                    Prefab = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
                    PrefabModel = EditorGUIUtility.IconContent("PrefabModel Icon").image as Texture2D;
                    PrefabVariant = EditorGUIUtility.IconContent("PrefabVariant Icon").image as Texture2D;
                    ScriptableObject = EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;
                    GameObject = EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
                }
            }

            public static Texture2D GetAssetIcon(AssetType type)
            {
                Affirm();

                if (type == AssetType.Scene)
                {
                    return Scene;
                }
                else if (type == AssetType.Prefab)
                {
                    return Prefab;
                }
                else if (type == AssetType.PrefabModel)
                {
                    return PrefabModel;
                }
                else if (type == AssetType.PrefabVariant)
                {
                    return PrefabVariant;
                }
                else if (type == AssetType.ScriptableObject)
                {
                    return ScriptableObject;
                }
                else
                {
                    throw new ArgumentException("Unrecognized AssetType: " + type, "type");
                }
            }

            public static Texture2D GetComponentIcon(Component component)
            {
                return AssetDatabase.GetCachedIcon(component.ScriptPath) as Texture2D;
            }
        }

        private void SetStatus(string text)
        {
            status = new GUIContent(text, EditorGUIUtility.IconContent("console.infoicon.sml").image);
        }

        private void OnGUI()
        {
            Styles.Affirm();

            float buttonHeight = EditorGUIUtility.singleLineHeight * 2;

            // Task List
            using (var scope = new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                taskView.DrawLayout(scope.rect);
            }

            // Selected Task
            if (selectedTask != null)
            {
                Asset asset = assets[selectedTask.AssetIndex];
                Component component = components[selectedTask.ComponentIndex];

                DrawSelectableLabel(selectedTask.PlainDescription(), EditorStyles.wordWrappedLabel);

                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField(AssetContent,
                        new GUIContent(asset.Path, Icons.GetAssetIcon(asset.Type)));
                    EditorGUILayout.LabelField(ComponentTypeContent,
                        new GUIContent(component.Type, Icons.GetComponentIcon(component)));

                    if (!string.IsNullOrEmpty(component.Path))
                    {
                        EditorGUILayout.LabelField(GameObjectContent, new GUIContent(component.Path, Icons.GameObject));
                    }

                    if (selectedTask.IsManual())
                    {
                        Rect buttonsRect = EditorGUILayout.GetControlRect(false, buttonHeight);
                        buttonsRect = EditorGUI.IndentedRect(buttonsRect);

                        GUIContent openScriptContent = new GUIContent("Open " + component.ScriptPath);

                        Rect openScriptRect = buttonsRect;
                        openScriptRect.width = GUI.skin.button.CalcSize(openScriptContent).x;

                        if (GUI.Button(openScriptRect, openScriptContent))
                        {
                            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(component.ScriptPath);
                            AssetDatabase.OpenAsset(script);
                        }

                        GUIContent viewDocumentationContent = new GUIContent("View Documentation");

                        Rect viewDocumentationRect = buttonsRect;
                        viewDocumentationRect.x = openScriptRect.xMax + GUI.skin.button.margin.left;
                        viewDocumentationRect.width = GUI.skin.button.CalcSize(viewDocumentationContent).x;

                        if (GUI.Button(viewDocumentationRect, viewDocumentationContent))
                        {
                            EditorUtils.OpenOnlineDocumentation("unity", "tools", "manual-tasks");
                        }

                        using (var scope = new EditorGUILayout.ScrollViewScope(manualDescriptionScrollPosition, GUILayout.Height(100)))
                        {
                            manualDescriptionScrollPosition = scope.scrollPosition;

                            DrawSelectableLabel(selectedTask.ManualInstructions(component), EditorStyles.wordWrappedLabel);
                        }
                    }
                    else
                    {
                        GUIContent buttonContent = new GUIContent("Execute");

                        Rect buttonRect = EditorGUILayout.GetControlRect(false, buttonHeight);
                        buttonRect.width = EditorGUIUtility.labelWidth;
                        buttonRect = EditorGUI.IndentedRect(buttonRect);

                        if (GUI.Button(buttonRect, buttonContent))
                        {
                            ExecuteTask(selectedTask, SavePolicy.AskToSave);
                        }
                    }
                }
            }

            // Status
            if (IsProcessing)
            {
                DrawProgressBar("Prefabs", prefabProgress);
                DrawProgressBar("ScriptableObjects", scriptableObjectProgress);
                DrawProgressBar("Scenes", sceneProgress);
            }

            GUILayout.Label(status, Styles.RichTextBox);

            // Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(buttonHeight)))
                {
                    Cancel();
                }

                using (new EditorGUI.DisabledScope(IsProcessing))
                {
                    if (GUILayout.Button(SearchButtonText, GUILayout.Height(buttonHeight)))
                    {
                        BeginSearching();
                    }

                    using (new EditorGUI.DisabledScope(executableTaskCount == 0))
                    {
                        if (GUILayout.Button(ExecuteButtonText(), GUILayout.Height(buttonHeight)))
                        {
                            BeginExecuting();
                        }
                    }
                }
            }

            if (focusedWindow == this
                && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
                Cancel();
                Event.current.Use();
            }
        }

        private static void DrawProgressBar(string label, SearchProgress progress)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, progress.Fraction(), label);
        }

        private static void DrawSelectableLabel(string text, GUIStyle style)
        {
            float height = style.CalcHeight(new GUIContent(text), EditorGUIUtility.currentViewWidth);

            EditorGUILayout.SelectableLabel(text, style, GUILayout.Height(height));
        }

        class TaskView : TreeView
        {
            private List<Task> tasks;
            private List<Asset> assets;
            private List<Component> components;

            public delegate void TaskEventHandler(Task task);

            public event TaskEventHandler taskSelected;
            public event TaskEventHandler taskDoubleClicked;
            public event TaskEventHandler taskEnableStateChanged;

            public delegate void AssetEventHandler(Asset asset);

            public event AssetEventHandler assetEnableStateChanged;

            public TaskView(TreeViewState state, List<Task> tasks, List<Asset> assets, List<Component> components)
                : base(state, new MultiColumnHeader(CreateHeaderState()))
            {
                this.tasks = tasks;
                this.assets = assets;
                this.components = components;

                showAlternatingRowBackgrounds = true;
                showBorder = true;

                multiColumnHeader.ResizeToFit();
            }

            public static MultiColumnHeaderState CreateHeaderState()
            {
                MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[] {
                    new MultiColumnHeaderState.Column()
                    {
                        headerContent = new GUIContent("Target"),
                        width = 225,
                        autoResize = false,
                        allowToggleVisibility = false,
                        canSort = false,
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("Task"),
                        autoResize = true,
                        allowToggleVisibility = false,
                        canSort = false,
                    },
                    new MultiColumnHeaderState.Column()
                    {
                        headerContent = new GUIContent("Status"),
                        width = 175,
                        autoResize = false,
                        allowToggleVisibility = false,
                        canSort = false,
                    }
                };

                return new MultiColumnHeaderState(columns);
            }

            public void DrawLayout(Rect rect)
            {
                extraSpaceBeforeIconAndLabel = ToggleWidth();

                OnGUI(rect);
            }

            public enum Column
            {
                Asset,
                Task,
                Status,
            }

            private class AssetItem : TreeViewItem
            {
                public Asset asset;
            }

            private class TaskItem : TreeViewItem
            {
                public Task task;
            }

            protected override TreeViewItem BuildRoot()
            {
                TreeViewItem root = new TreeViewItem(-1, -1);

                if (tasks.Count > 0)
                {
                    int index = 0;

                    AssetItem assetItem = null;

                    foreach (Task task in tasks)
                    {
                        Asset asset = assets[task.AssetIndex];

                        if (assetItem == null || assetItem.asset != asset)
                        {
                            assetItem = new AssetItem() {
                                id = index++,
                                asset = asset,
                                displayName = asset.Path,
                                icon = Icons.GetAssetIcon(asset.Type),
                            };

                            root.AddChild(assetItem);
                        }

                        TreeViewItem taskItem = new TaskItem() {
                            id = index++,
                            task = task,
                        };

                        assetItem.AddChild(taskItem);
                    }
                }
                else
                {
                    TreeViewItem item = new TreeViewItem(0);
                    item.displayName = "No tasks.";

                    root.AddChild(item);
                }

                SetupDepthsFromParentsAndChildren(root);

                return root;
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                return false;
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                base.SelectionChanged(selectedIds);

                if (taskSelected != null)
                {
                    if (selectedIds.Count > 0)
                    {
                        TaskItem item = FindItem(selectedIds[0], rootItem) as TaskItem;

                        if (item != null)
                        {
                            taskSelected(item.task);
                            return;
                        }
                    }

                    taskSelected(null);
                }
            }

            protected override void SingleClickedItem(int id)
            {
                TreeViewItem item = FindItem(id, rootItem);

                if (!(item is TaskItem))
                {
                    SetExpanded(id, !IsExpanded(id));
                }
                else
                {
                    base.SingleClickedItem(id);
                }
            }

            protected override void DoubleClickedItem(int id)
            {
                if (taskDoubleClicked != null)
                {
                    TaskItem item = FindItem(id, rootItem) as TaskItem;

                    if (item == null)
                    {
                        return;
                    }

                    taskDoubleClicked(item.task);
                }
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                TreeViewItem item = args.item;

                if (item is TaskItem)
                {
                    Task task = (item as TaskItem).task;

                    Rect toggleRect = args.rowRect;
                    toggleRect.x = GetContentIndent(item);
                    toggleRect.width = ToggleWidth();

                    TaskToggle(toggleRect, task);

                    for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                    {
                        Rect rect = args.GetCellRect(i);

                        if (i == 0)
                        {
                            rect.xMin = toggleRect.xMax;
                        }

                        CellGUI(rect, task, args.GetColumn(i), args.selected, args.focused);
                    }
                }
                else if (item is AssetItem)
                {
                    base.RowGUI(args);

                    Rect rect = args.rowRect;
                    rect.x = GetContentIndent(item);
                    rect.width = ToggleWidth();

                    AssetToggle(rect, (item as AssetItem).asset);
                }
                else
                {
                    base.RowGUI(args);
                }
            }

            private static float ToggleWidth()
            {
                return GUI.skin.toggle.CalcSize(GUIContent.none).x;
            }

            private void AssetToggle(Rect rect, Asset asset)
            {
                using (var scope = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUI.showMixedValue = (asset.EnableState == EnableState.Mixed);

                    bool enabled = EditorGUI.Toggle(rect, asset.EnableState == EnableState.Enabled);

                    EditorGUI.showMixedValue = false;

                    if (scope.changed)
                    {
                        asset.EnableState = enabled ? EnableState.Enabled : EnableState.Disabled;

                        if (assetEnableStateChanged != null)
                        {
                            assetEnableStateChanged(asset);
                        }
                    }
                }
            }

            private void TaskToggle(Rect rect, Task task)
            {
                if (!task.IsManual())
                {
                    using (var scope = new EditorGUI.ChangeCheckScope())
                    {
                        task.Enabled = EditorGUI.Toggle(rect, task.Enabled);

                        if (scope.changed && taskEnableStateChanged != null)
                        {
                            taskEnableStateChanged(task);
                        }
                    }
                }
            }

            private void CellGUI(Rect rect, Task task, int columnIndex, bool selected, bool focused)
            {
                Component component = components[task.ComponentIndex];

                switch ((Column)columnIndex)
                {
                    case Column.Asset:
                        if (Event.current.type == EventType.Repaint)
                        {
                            Texture2D typeIcon = Icons.GetComponentIcon(components[task.ComponentIndex]);

                            using (new GUI.GroupScope(rect))
                            {
                                Rect iconRect = new Rect(0, 0, rect.height, rect.height);

                                GUI.DrawTexture(iconRect, typeIcon, ScaleMode.ScaleToFit);

                                GUIContent type = new GUIContent(component.Type);

                                bool hasGameObjectPath = !string.IsNullOrEmpty(component.Path);

                                if (hasGameObjectPath)
                                {
                                    type.text += " on";
                                }

                                Rect typeRect = new Rect(iconRect.xMax, 0,
                                    DefaultStyles.label.CalcSize(type).x, rect.height);

                                DefaultGUI.Label(typeRect, type.text, selected, focused);

                                if (hasGameObjectPath)
                                {
                                    iconRect.x = typeRect.xMax;

                                    GUI.DrawTexture(iconRect, Icons.GameObject, ScaleMode.ScaleToFit);

                                    GUIContent gameObject = new GUIContent(component.Path);

                                    Rect gameObjectRect = new Rect(iconRect.xMax, 0,
                                        DefaultStyles.label.CalcSize(gameObject).x, rect.height);

                                    DefaultGUI.Label(gameObjectRect, gameObject.text, selected, focused);
                                }
                            }
                        }

                        break;
                    case Column.Task:
                        if (Event.current.type == EventType.Repaint)
                        {
                            string text = task.ToString();

                            if (task.IsManual())
                            {
                                text = "Manual task: " + text;
                            }

                            Styles.TreeViewRichText.Draw(rect, text, false, false, selected, focused);
                        }
                        break;
                    case Column.Status:
                        if (Event.current.type == EventType.Repaint)
                        {
                            if (task.IsManual())
                            {
                                DefaultGUI.Label(rect, "Manual Changes Required", selected, focused);
                            }
                            else
                            {
                                DefaultGUI.Label(rect, task.HasExecuted ? "Complete" : "Pending", selected, focused);
                            }
                        }
                        break;
                }
            }
        }
    }
}
