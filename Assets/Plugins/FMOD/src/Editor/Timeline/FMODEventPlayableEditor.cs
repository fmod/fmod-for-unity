#if (UNITY_TIMELINE_EXIST || !UNITY_2019_1_OR_NEWER)

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using System;
using System.Linq;
using System.Reflection;

namespace FMODUnity
{
    [CustomEditor(typeof(FMODEventPlayable))]
    public class FMODEventPlayableEditor : Editor
    {
        private FMODEventPlayable eventPlayable;
        private EditorEventRef editorEventRef;
        private List<EditorParamRef> missingInitialParameterValues = new List<EditorParamRef>();
        private List<EditorParamRef> missingParameterAutomations = new List<EditorParamRef>();

        SerializedProperty parametersProperty;
        SerializedProperty parameterLinksProperty;
        SerializedProperty parameterAutomationProperty;

        ListView parameterLinksView;
        ListView initialParameterValuesView;

        public void OnEnable()
        {
            eventPlayable = target as FMODEventPlayable;

            parametersProperty = serializedObject.FindProperty("template.parameters");
            parameterLinksProperty = serializedObject.FindProperty("template.parameterLinks");
            parameterAutomationProperty = serializedObject.FindProperty("template.parameterAutomation");

            parameterLinksView = new ListView(parameterLinksProperty);
            parameterLinksView.drawElementWithLabelCallback = DrawParameterLink;
            parameterLinksView.onCanAddCallback = (list) => missingParameterAutomations.Count > 0;
            parameterLinksView.onAddDropdownCallback = DoAddParameterLinkMenu;
            parameterLinksView.onRemoveCallback = (list) => DeleteParameterAutomation(list.index);

            initialParameterValuesView = new ListView(parametersProperty);
            initialParameterValuesView.drawElementWithLabelCallback = DrawInitialParameterValue;
            initialParameterValuesView.onCanAddCallback = (list) => missingInitialParameterValues.Count > 0;
            initialParameterValuesView.onAddDropdownCallback = DoAddInitialParameterValueMenu;
            initialParameterValuesView.onRemoveCallback = (list) => DeleteInitialParameterValue(list.index);

            RefreshEventRef();

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            RefreshMissingParameterLists();

            // This is in case the undo/redo modified any curves on the Playable's clip
            RefreshTimelineEditor();
        }

        string eventName;

        private void RefreshEventRef()
        {
            if (eventName != eventPlayable.eventName)
            {
                eventName = eventPlayable.eventName;

                if (!string.IsNullOrEmpty(eventName))
                {
                    editorEventRef = EventManager.EventFromPath(eventName);
                }
                else
                {
                    editorEventRef = null;
                }

                if (editorEventRef != null)
                {
                    eventPlayable.UpdateEventDuration(
                        editorEventRef.IsOneShot ? editorEventRef.Length : float.PositiveInfinity);
                }

                ValidateParameterSettings();
                RefreshMissingParameterLists();
            }
        }

        private void ValidateParameterSettings()
        {
            if (editorEventRef != null)
            {
                List<string> namesToDelete = new List<string>();

                for (int i = 0; i < parametersProperty.arraySize; ++i)
                {
                    SerializedProperty current = parametersProperty.GetArrayElementAtIndex(i);
                    SerializedProperty name = current.FindPropertyRelative("Name");

                    EditorParamRef paramRef = editorEventRef.LocalParameters.FirstOrDefault(p => p.Name == name.stringValue);

                    if (paramRef != null)
                    {
                        SerializedProperty value = current.FindPropertyRelative("Value");
                        value.floatValue = Mathf.Clamp(value.floatValue, paramRef.Min, paramRef.Max);
                    }
                    else
                    {
                        namesToDelete.Add(name.stringValue);
                    }
                }

                foreach(string name in namesToDelete)
                {
                    DeleteInitialParameterValue(name);
                }

                namesToDelete.Clear();

                for (int i = 0; i < parameterLinksProperty.arraySize; ++i)
                {
                    SerializedProperty current = parameterLinksProperty.GetArrayElementAtIndex(i);
                    SerializedProperty name = current.FindPropertyRelative("Name");

                    if (!editorEventRef.LocalParameters.Any(p => p.Name == name.stringValue))
                    {
                        namesToDelete.Add(name.stringValue);
                    }
                }

                foreach(string name in namesToDelete)
                {
                    DeleteParameterAutomation(name);
                }
            }
        }

        private void RefreshMissingParameterLists()
        {
            if (editorEventRef != null)
            {
                serializedObject.Update();

                missingInitialParameterValues =
                    editorEventRef.LocalParameters.Where(p => !InitialParameterValueExists(p.Name)).ToList();
                missingParameterAutomations =
                    editorEventRef.LocalParameters.Where(p => !ParameterLinkExists(p.Name)).ToList();
            }
            else
            {
                missingInitialParameterValues.Clear();
                missingParameterAutomations.Clear();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RefreshEventRef();

            var ev = serializedObject.FindProperty("eventName");
            var stopType = serializedObject.FindProperty("stopType");

            EditorGUILayout.PropertyField(ev, new GUIContent("Event"));
            EditorGUILayout.PropertyField(stopType, new GUIContent("Stop Mode"));

            DrawInitialParameterValues();
            DrawParameterAutomations();

            eventPlayable.OnValidate();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawInitialParameterValues()
        {
            if (editorEventRef != null)
            {
                parametersProperty.isExpanded =
                    EditorGUILayout.Foldout(parametersProperty.isExpanded, "Initial Parameter Values", true);

                if (parametersProperty.isExpanded)
                {
                    initialParameterValuesView.DrawLayout();
                }
            }
        }

        void DoAddInitialParameterValueMenu(Rect rect, UnityEditorInternal.ReorderableList list)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("All"), false, () =>
                {
                    foreach (EditorParamRef parameter in missingInitialParameterValues)
                    {
                        AddInitialParameterValue(parameter);
                    }
                });

            menu.AddSeparator(string.Empty);

            foreach (EditorParamRef parameter in missingInitialParameterValues)
            {
                string text = parameter.Name;

                if (ParameterLinkExists(parameter.Name))
                {
                    text += " (automated)";
                }

                menu.AddItem(new GUIContent(text), false,
                    (userData) =>
                    {
                        AddInitialParameterValue(userData as EditorParamRef);
                    },
                    parameter);
            }

            menu.DropDown(rect);
        }

        void DrawInitialParameterValue(Rect rect, float labelRight, int index, bool active, bool focused)
        {
            if (editorEventRef == null)
            {
                return;
            }

            SerializedProperty property = parametersProperty.GetArrayElementAtIndex(index);

            string name = property.FindPropertyRelative("Name").stringValue;

            EditorParamRef paramRef = editorEventRef.LocalParameters.FirstOrDefault(p => p.Name == name);

            if (paramRef == null)
            {
                return;
            }

            Rect nameLabelRect = rect;
            nameLabelRect.xMax = labelRight;

            Rect sliderRect = rect;
            sliderRect.xMin = nameLabelRect.xMax;

            SerializedProperty valueProperty = property.FindPropertyRelative("Value");

            GUI.Label(nameLabelRect, name);

            using (new NoIndentScope())
            {
                valueProperty.floatValue =
                    EditorGUI.Slider(sliderRect, valueProperty.floatValue, paramRef.Min, paramRef.Max);
            }
        }

        void DrawParameterAutomations()
        {
            if (editorEventRef != null)
            {
                parameterLinksProperty.isExpanded =
                    EditorGUILayout.Foldout(parameterLinksProperty.isExpanded, "Parameter Automations", true);

                if (parameterLinksProperty.isExpanded)
                {
                    parameterLinksView.DrawLayout();
                }
            }
        }

        void DoAddParameterLinkMenu(Rect rect, UnityEditorInternal.ReorderableList list)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("All"), false, () =>
                {
                    foreach (EditorParamRef parameter in missingParameterAutomations)
                    {
                        AddParameterAutomation(parameter.Name);
                    }
                });

            menu.AddSeparator(string.Empty);

            foreach (EditorParamRef parameter in missingParameterAutomations)
            {
                string text = parameter.Name;

                if (InitialParameterValueExists(parameter.Name))
                {
                    text += " (has initial value)";
                }

                menu.AddItem(new GUIContent(text), false,
                    (userData) =>
                    {
                        AddParameterAutomation(userData as string);
                    },
                    parameter.Name);
            }

            menu.DropDown(rect);
        }

        void DrawParameterLink(Rect rect, float labelRight, int index, bool active, bool focused)
        {
            if (editorEventRef == null)
            {
                return;
            }

            SerializedProperty linkProperty = parameterLinksProperty.GetArrayElementAtIndex(index);

            string name = linkProperty.FindPropertyRelative("Name").stringValue;

            EditorParamRef paramRef = editorEventRef.LocalParameters.FirstOrDefault(p => p.Name == name);

            if (paramRef == null)
            {
                return;
            }

            int slot = linkProperty.FindPropertyRelative("Slot").intValue;

            string slotName = string.Format("slot{0:D2}", slot);
            SerializedProperty valueProperty = parameterAutomationProperty.FindPropertyRelative(slotName);

            GUIStyle slotStyle = GUI.skin.label;

            Rect slotRect = rect;
            slotRect.width = slotStyle.CalcSize(new GUIContent("slot 00:")).x;

            Rect nameRect = rect;
            nameRect.xMin = slotRect.xMax;
            nameRect.xMax = labelRight;

            Rect valueRect = rect;
            valueRect.xMin = nameRect.xMax;

            using (new EditorGUI.PropertyScope(rect, GUIContent.none, valueProperty))
            {
                GUI.Label(slotRect, string.Format("slot {0:D2}:", slot), slotStyle);
                GUI.Label(nameRect, name);

                using (new NoIndentScope())
                {
                    valueProperty.floatValue =
                        EditorGUI.Slider(valueRect, valueProperty.floatValue, paramRef.Min, paramRef.Max);
                }
            }
        }

        bool InitialParameterValueExists(string name)
        {
            return parametersProperty.ArrayContains("Name", p => p.stringValue == name);
        }

        bool ParameterLinkExists(string name)
        {
            return parameterLinksProperty.ArrayContains("Name", p => p.stringValue == name);
        }

        void AddInitialParameterValue(EditorParamRef editorParamRef)
        {
            serializedObject.Update();

            if (!InitialParameterValueExists(editorParamRef.Name))
            {
                DeleteParameterAutomation(editorParamRef.Name);

                parametersProperty.ArrayAdd(p => {
                    p.FindPropertyRelative("Name").stringValue = editorParamRef.Name;
                    p.FindPropertyRelative("Value").floatValue = editorParamRef.Default;
                });

                serializedObject.ApplyModifiedProperties();

                RefreshMissingParameterLists();
            }
        }

        void DeleteInitialParameterValue(string name)
        {
            serializedObject.Update();

            int index = parametersProperty.FindArrayIndex("Name", p => p.stringValue == name);

            if (index >= 0)
            {
                DeleteInitialParameterValue(index);
            }
        }

        void DeleteInitialParameterValue(int index)
        {
            serializedObject.Update();

            parametersProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();
            RefreshMissingParameterLists();
        }

        void AddParameterAutomation(string name)
        {
            serializedObject.Update();

            if (!ParameterLinkExists(name))
            {
                int slot = -1;

                for (int i = 0; i < AutomatableSlots.Count; ++i)
                {
                    if (!parameterLinksProperty.ArrayContains("Slot", p => p.intValue == i))
                    {
                        slot = i;
                        break;
                    }
                }

                if (slot >= 0)
                {
                    DeleteInitialParameterValue(name);

                    parameterLinksProperty.ArrayAdd(p => {
                        p.FindPropertyRelative("Name").stringValue = name;
                        p.FindPropertyRelative("Slot").intValue = slot;
                    });

                    serializedObject.ApplyModifiedProperties();

                    RefreshMissingParameterLists();
                    RefreshTimelineEditor();
                }
            }
        }

        static bool ClipHasCurves(TimelineClip clip)
        {
#if UNITY_2019_OR_NEWER
            return clip.hasCurves;
#else
            return clip.curves != null && !clip.curves.empty;
#endif
        }

        void DeleteParameterAutomation(string name)
        {
            serializedObject.Update();

            int index = parameterLinksProperty.FindArrayIndex("Name", p => p.stringValue == name);

            if (index >= 0)
            {
                DeleteParameterAutomation(index);
            }
        }

        void DeleteParameterAutomation(int index)
        {
            serializedObject.Update();

            if (ClipHasCurves(eventPlayable.OwningClip))
            {
                SerializedProperty linkProperty = parameterLinksProperty.GetArrayElementAtIndex(index);
                SerializedProperty slotProperty = linkProperty.FindPropertyRelative("Slot");

                AnimationClip curvesClip = eventPlayable.OwningClip.curves;

                Undo.RecordObject(curvesClip, string.Empty);
                AnimationUtility.SetEditorCurve(curvesClip, GetParameterCurveBinding(slotProperty.intValue), null);
            }

            parameterLinksProperty.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();

            RefreshMissingParameterLists();

            RefreshTimelineEditor();
        }

        static EditorCurveBinding GetParameterCurveBinding(int index)
        {
            EditorCurveBinding result = new EditorCurveBinding() {
                path = string.Empty,
                type = typeof(FMODEventPlayable),
                propertyName = string.Format("parameterAutomation.slot{0:D2}", index),
            };

            return result;
        }

        static void RefreshTimelineEditor()
        {
#if UNITY_2018_3_OR_NEWER
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
#else
            object[] noParameters = new object[] { };

            Type timelineType = typeof(TimelineEditor);

            Assembly assembly = timelineType.Assembly;
            Type windowType = assembly.GetType("UnityEditor.Timeline.TimelineWindow");

            PropertyInfo windowInstanceProperty = windowType.GetProperty("instance");
            object windowInstance = windowInstanceProperty.GetValue(null, noParameters);

            if (windowInstance == null)
            {
                return;
            }

            PropertyInfo windowStateProperty = windowType.GetProperty("state");
            object windowState = windowStateProperty.GetValue(windowInstance, noParameters);

            if (windowState == null)
            {
                return;
            }

            Type windowStateType = windowState.GetType();
            MethodInfo refreshMethod = windowStateType.GetMethod("Refresh", new Type[] { });

            refreshMethod.Invoke(windowState, noParameters);
#endif
        }
    }
}
#endif