using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FMODUnity
{
    public class SetupWizardWindow : EditorWindow
    {
        private static SetupWizardWindow instance;

        enum PAGES : int
        {
            Welcome = 0,
            Linking,
            Listener,
            UnityAudio,
            UnitySources,
            SourceControl,
            End,
            Max
        }

        List<string> pageNames = new List<string>
        {
            "Welcome",
            "Linking",
            "Listener",
            "Unity Audio",
            "Unity Sources",
            "Source Control",
            "End"
        };

        List<bool> pageComplete = new List<bool>(new bool[(int) PAGES.Max]);

        PAGES currentPage = PAGES.Welcome;

        AudioListener[] unityListeners;
        StudioListener[] fmodListeners;
        Vector2 scroll1, scroll2;
        bool bFoundUnityListener;
        bool bFoundFmodListener;

        AudioSource[] unityAudioSources;

        GUIStyle titleStyleLeft;
        GUIStyle titleStyleCenter;
        GUIStyle bodyStyle;
        GUIStyle buttonStyle;
        GUIStyle navButtonStyle;
        GUIStyle sourceButtonStyle;
        GUIStyle crumbStyle;
        Color crumbDefault;
        Color crumbHighlight;

        const string logoBlack = "FMOD/FMODLogoBlack.png";
        const string logoWhite = "FMOD/FMODLogoWhite.png";

        Texture2D logoTexture;
        Texture2D tickTexture;
        Texture2D crossTexture;
        GUIStyle iconStyle;

        const string backButtonText = "Back";

        SimpleTreeView m_SimpleTreeView;
        TreeViewState m_TreeViewState;

        bool bStudioLinked;

        public static void Startup()
        {
            if (!Settings.Instance.HideSetupWizard)
            {
                ShowAssistant();
            }
        }

        [MenuItem("FMOD/Setup Wizard")]
        public static void ShowAssistant()
        {
            instance = (SetupWizardWindow)GetWindow(typeof(SetupWizardWindow), true, "FMOD Setup Wizard");
            instance.ShowUtility();
            instance.minSize = new Vector2(750, 500);
            instance.maxSize = instance.minSize;
            var position = new Rect(Vector2.zero, instance.minSize);
            Vector2 screenCenter = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) / 2;
            position.center = screenCenter / EditorGUIUtility.pixelsPerPoint;
            instance.position = position;
        }

        private void OnEnable()
        {
            logoTexture = EditorGUIUtility.Load(EditorGUIUtility.isProSkin ? logoWhite : logoBlack) as Texture2D;

            crossTexture = EditorGUIUtility.Load("FMOD/CrossYellow.png") as Texture2D;
            tickTexture = EditorGUIUtility.Load("FMOD/TickGreen.png") as Texture2D;

            titleStyleLeft = new GUIStyle();
            titleStyleLeft.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            titleStyleLeft.wordWrap = true;
            titleStyleLeft.fontStyle = FontStyle.Bold;

            titleStyleCenter = new GUIStyle(titleStyleLeft);
            titleStyleCenter.alignment = TextAnchor.MiddleCenter;

            titleStyleLeft.alignment = TextAnchor.MiddleLeft;
            bodyStyle = new GUIStyle(titleStyleLeft);
            crumbStyle = new GUIStyle(titleStyleLeft);
            crumbDefault = EditorGUIUtility.isProSkin ? Color.gray : Color.gray;
            crumbHighlight = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            scroll1 = scroll2 = new Vector2();

            iconStyle = new GUIStyle();
            iconStyle.alignment = TextAnchor.MiddleCenter;

            CheckStudioLinked();
            CheckListeners();
            CheckSources();
            CheckUnityAudio();
        }

        void OnGUI()
        {
            buttonStyle = new GUIStyle("Button");
            buttonStyle.fixedHeight = 30;
            // Draw Header
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Box(logoTexture, titleStyleLeft);
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                Breadcrumbs();

                // Draw Body
                using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandHeight(true)))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();

                    switch (currentPage)
                    {
                        case PAGES.Welcome: WelcomePage(); break;
                        case PAGES.Linking: LinkingPage(); break;
                        case PAGES.Listener: ListenerPage(); break;
                        case PAGES.UnityAudio: DisableUnityAudioPage(); break;
                        case PAGES.UnitySources: UnitySources(); break;
                        case PAGES.SourceControl: SourceControl(); break;
                        case PAGES.End: EndPage(); break;
                    }

                    Buttons();

                    if (currentPage == PAGES.Welcome)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();

                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                bool hide = Settings.Instance.HideSetupWizard;

                                hide = EditorGUILayout.Toggle("Do not display this again", hide);

                                if (check.changed)
                                {
                                    Settings.Instance.HideSetupWizard = hide;
                                    EditorUtility.SetDirty(Settings.Instance);
                                }
                            }
                            GUILayout.FlexibleSpace();
                        }
                    }
                    else
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.Space();
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                }
            }
        }

        private void OnInspectorUpdate()
        {
            switch (currentPage)
            {
                case PAGES.Welcome:                         break;
                case PAGES.Linking:     CheckStudioLinked();break;
                case PAGES.Listener:    CheckListeners();   break;
                case PAGES.UnityAudio:                      break;
                case PAGES.UnitySources:CheckSources();     break;
                case PAGES.End:                             break;
                case PAGES.Max:                             break;
                default:                                    break;
            }
        }

        void CheckStudioLinked()
        {
            pageComplete[(int)PAGES.Linking] = IsStudioLinked();
        }

        bool IsStudioLinked()
        {
            return !string.IsNullOrEmpty(Settings.Instance.SourceBankPath);
        }

        void CheckListeners()
        {
            var UListeners = Resources.FindObjectsOfTypeAll<AudioListener>();
            var FListeners = Resources.FindObjectsOfTypeAll<StudioListener>();
            if ((unityListeners == null || fmodListeners == null) || (!unityListeners.SequenceEqual(UListeners) || !fmodListeners.SequenceEqual(FListeners)))
            {
                unityListeners = UListeners;
                bFoundUnityListener = unityListeners.Length > 0;
                fmodListeners = FListeners;
                bFoundFmodListener = fmodListeners.Length > 0;
                Repaint();
            }
            pageComplete[(int)PAGES.Listener] = (!bFoundUnityListener && bFoundFmodListener);
        }

        void CheckSources()
        {
            var ASources = Resources.FindObjectsOfTypeAll<AudioSource>();
            if (unityAudioSources == null || !ASources.SequenceEqual(unityAudioSources))
            {
                unityAudioSources = ASources;
                if (m_SimpleTreeView != null && unityAudioSources.Length > 0)
                {
                    m_SimpleTreeView.Reload();
                }
            }
            pageComplete[(int)PAGES.UnitySources] = ASources != null ? (ASources.Length == 0) : true;
        }

        void CheckUnityAudio()
        {
            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var serializedManager = new SerializedObject(audioManager);
            var prop = serializedManager.FindProperty("m_DisableAudio");
            pageComplete[(int)PAGES.UnityAudio] = prop.boolValue;
        }

        void WelcomePage()
        {

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Welcome to FMOD for Unity!", titleStyleCenter);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("This setup wizard will help you add FMOD to your game.", titleStyleCenter);
            GUILayout.FlexibleSpace();
        }

        void Breadcrumbs()
        {
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(150)))
            {
                crumbStyle.alignment = TextAnchor.MiddleCenter;
                Color oldColor = GUI.backgroundColor;
                EditorGUILayout.Space();

                for (int i = 0; i < pageNames.Count; i++)
                {
                    if (i > 0 && i < pageNames.Count - 1)
                    {
                        GUI.backgroundColor = (pageComplete[i] ? Color.green : Color.yellow);
                    }
                    crumbStyle.normal.textColor = (i == (int)currentPage ? crumbHighlight : crumbDefault);
                    using (var b = new EditorGUILayout.HorizontalScope("button", GUILayout.Height(22)))
                    {
                        if (GUI.Button(b.rect, pageNames[i], crumbStyle))
                        {
                            currentPage = (PAGES)i;
                        }
                        EditorGUILayout.Space();
                    }
                    GUI.backgroundColor = oldColor;
                }
                GUILayout.FlexibleSpace();
            }
        }

        void LinkingPage()
        {
            EditorGUILayout.LabelField("In order to access your FMOD Studio content you need to locate the FMOD Studio Project or the .bank files that FMOD Studio produces, and configure a few other settings.", titleStyleLeft);
            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Choose how to access your FMOD Studio content:", titleStyleLeft);

            var descStyle = new GUIStyle(titleStyleLeft);
            descStyle.fontStyle = FontStyle.Normal;
            descStyle.alignment = TextAnchor.MiddleLeft;
            descStyle.margin = new RectOffset(5,0,0,0);

            EditorGUILayout.Space(); 

            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope("box"))
                {
                    float indent = 25;
                    sourceButtonStyle = new GUIStyle("button");
                    sourceButtonStyle.fixedWidth = 150;
                    sourceButtonStyle.fixedHeight = 35;
                    sourceButtonStyle.margin = new RectOffset();
                    var serializedObject = new SerializedObject(Settings.Instance);

                    var boxStyle = new GUIStyle();
                    boxStyle.fixedHeight = 10;
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indent);
                        if (GUILayout.Button("FMOD Studio Project", sourceButtonStyle))
                        {
                            SettingsEditor.BrowseForSourceProjectPath(serializedObject);
                        }
                        GUILayout.Label("If you have the complete FMOD Studio Project.", descStyle, GUILayout.Height(sourceButtonStyle.fixedHeight));
                    }
                    EditorGUILayout.Space();
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indent);
                        if (GUILayout.Button("Single Platform Build", sourceButtonStyle))
                        {
                            SettingsEditor.BrowseForSourceBankPath(serializedObject);
                        }
                        EditorGUILayout.LabelField("If you have the contents of the Build folder for a single platform.", descStyle, GUILayout.Height(sourceButtonStyle.fixedHeight));
                        GUILayout.FlexibleSpace();
                    }
                    EditorGUILayout.Space();
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indent);
                        if (GUILayout.Button("Multiple Platform Build", sourceButtonStyle))
                        {
                            SettingsEditor.BrowseForSourceBankPath(serializedObject, true);
                        }
                        EditorGUILayout.LabelField("If you have the contents of the Build folder for multiple platforms, with each platform in its own subdirectory.", descStyle, GUILayout.Height(sourceButtonStyle.fixedHeight));
                    }
                }
            }

            if (IsStudioLinked())
            {
                EditorGUILayout.Space();

                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;

                using (new GUILayout.HorizontalScope("box"))
                {
                    GUILayout.FlexibleSpace();

                    GUILayout.Label(tickTexture, iconStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));

                    EditorGUILayout.Space();

                    using (new GUILayout.VerticalScope())
                    {
                        Settings settings = Settings.Instance;

                        if (settings.HasSourceProject)
                        {
                            EditorGUILayout.LabelField("Using the FMOD Studio project at:", descStyle);
                            EditorGUILayout.LabelField(settings.SourceProjectPath, descStyle);
                        }
                        else if (settings.HasPlatforms)
                        {
                            EditorGUILayout.LabelField("Using the multiple platform build at:", descStyle);
                            EditorGUILayout.LabelField(settings.SourceBankPath, descStyle);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Using the single platform build at:", descStyle);
                            EditorGUILayout.LabelField(settings.SourceBankPath, descStyle);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                GUI.backgroundColor = oldColor;
            }

            GUILayout.FlexibleSpace();
        }

        void ListenerPage()
        {
            EditorGUILayout.LabelField("If you do not intend to use the built in Unity audio, you can choose to replace the Audio Listener with the FMOD Studio Listener.\n", bodyStyle);
            EditorGUILayout.LabelField("Adding the FMOD Studio Listener component to the main camera provides the FMOD Engine with the information it needs to play 3D events correctly.", bodyStyle);
            EditorGUILayout.Space();
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Display found objects containing Unity listeners
                DisplayListeners(unityListeners, ref scroll1);

                // Show FMOD Listeners
                DisplayListeners(fmodListeners, ref scroll2);

                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledGroupScope(!bFoundUnityListener))
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Replace Unity " + ((unityListeners != null && unityListeners.Length > 1) ? "Listeners" : "Listener") + " with FMOD Audio Listener.", buttonStyle))
                    {
                        for (int i = 0; i < unityListeners.Length; i++)
                        {
                            var listener = unityListeners[i];
                            if (listener)
                            {
                                Debug.Log("[FMOD Assistant] Replacing Unity Listener with FMOD Listener on " + listener.gameObject.name);
                                if (listener.GetComponent<StudioListener>() == null)
                                {
                                    listener.gameObject.AddComponent(typeof(StudioListener));
                                }
                                DestroyImmediate(unityListeners[i]);
                                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                                Repaint();
                            }
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        void DisplayListeners<T>(T[] listeners, ref Vector2 scrollPos)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                bool bUnityListenerType = false;
                if (typeof(T) == typeof(AudioListener))
                {
                    bUnityListenerType = true;
                }

                if (listeners != null && listeners.Length > 0)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(listeners.Length + " " + (bUnityListenerType ? "Unity" : "FMOD") + " " + (listeners.Length > 1 ? "Listeners" : "Listener") + " found.", titleStyleLeft, GUILayout.ExpandWidth(true));
                        GUILayout.FlexibleSpace();
                    }

                    using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.ExpandWidth(true)))
                    {
                        scrollPos = scrollView.scrollPosition;
                        foreach (T l in listeners)
                        {
                            var listener = l as Component;
                            if (listener != null && GUILayout.Button(listener.gameObject.name, GUILayout.ExpandWidth(true)))
                            {
                                Selection.activeGameObject = listener.gameObject;
                                EditorGUIUtility.PingObject(listener);
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField("No " + (bUnityListenerType ? "Unity" : "FMOD") + " Listeners found.", titleStyleLeft);
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        void DisableUnityAudioPage()
        {
            EditorGUILayout.LabelField("We recommend that you disable the built-in Unity audio for all platforms, to prevent it from consuming system audio resources that the FMOD Engine needs.", titleStyleLeft);
            GUILayout.FlexibleSpace();

            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var serializedManager = new SerializedObject(audioManager);
            var prop = serializedManager.FindProperty("m_DisableAudio");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledGroupScope(prop.boolValue))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(prop.boolValue ? "Built in audio has been disabled" : "Disable built in audio", buttonStyle))
                    {
                        prop.boolValue = true;
                        serializedManager.ApplyModifiedProperties();
                        Debug.Log("[FMOD Assistant] Built in Unity audio has been disabled.");
                        Repaint();
                    }

                }
                GUILayout.FlexibleSpace();
            }
            pageComplete[(int)PAGES.UnityAudio] = prop.boolValue;

            GUILayout.FlexibleSpace();
        }

        void UnitySources()
        {
            if (unityAudioSources != null && unityAudioSources.Length > 0)
            {
                EditorGUILayout.LabelField("Listed below are all the Unity Audio Sources found in the currently loaded scenes and the Assets directory.\nSelect an Audio Source and replace it with an FMOD Studio Event Emitter.", titleStyleLeft);
                EditorGUILayout.Space();

                if (m_SimpleTreeView == null)
                {
                    if (m_TreeViewState == null)
                    {
                        m_TreeViewState = new TreeViewState();
                    }
                    m_SimpleTreeView = new SimpleTreeView(m_TreeViewState);
                }

                m_SimpleTreeView.Drawlayout();
            }
            else
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No Unity Audio Sources have been found!", titleStyleCenter);
                GUILayout.FlexibleSpace();
            }
        }

        const string IgnoreFileText =
@"# Never ignore DLLs in the FMOD subfolder.
!/[Aa]ssets/Plugins/FMOD/**/lib/*

# Don't ignore images and gizmos used by FMOD in the Unity Editor.
!/[Aa]ssets/Gizmos/FMOD/*
!/[Aa]ssets/Editor Default Resources/FMOD/*
                    
# Ignore the Cache folder since it is updated locally.
/[Aa]ssets/Plugins/FMOD/Cache/*
                    
# Ignore bank files in the StreamingAssets folder.
/[Aa]ssets/StreamingAssets/**/*.bank
/[Aa]ssets/StreamingAssets/**/*.bank.meta
                    
# If the source bank files are kept outside of the StreamingAssets folder then these can be ignored.
# Log files can be ignored.
fmod_editor.log";

        void SourceControl()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("There are a number of files produced by FMOD for Unity that should be ignored by source control. " +
                "Here is an example of what you should add to your source control ignore file:", titleStyleLeft);
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.TextArea(IgnoreFileText);
                }
                GUILayout.FlexibleSpace();
            }
            pageComplete[(int)PAGES.SourceControl] = true;
        }

        void EndPage()
        {
            GUILayout.FlexibleSpace();
            bool completed = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope())
                {
                    for (int i = 1; i < pageNames.Count - 1; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(pageNames[i], titleStyleLeft);
                            EditorGUILayout.Space();
                            GUILayout.Label(pageComplete[i] ? tickTexture : crossTexture, iconStyle, GUILayout.ExpandWidth(false));

                            if (pageComplete[i] == false)
                            {
                                completed = false;
                            }
                        }
                        GUILayout.Space(8);
                    }
                }
                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(completed ? "FMOD for Unity has been set up successfully!" : "FMOD for Unity has not finished being set up.", titleStyleCenter);
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(" Integration Manual ", buttonStyle))
                {
                    EditorUtils.OnlineManual();
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(20);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(" FMOD Settings ", buttonStyle))
                {
                    Settings.EditSettings();
                }

                GUILayout.FlexibleSpace();
            }

            if (completed)
            {
                Settings.Instance.HideSetupWizard = true;
            }
        }

        void Buttons()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                navButtonStyle = new GUIStyle("Button");
                navButtonStyle.fixedHeight = 45;
                navButtonStyle.fixedWidth = 75;

                GUILayout.FlexibleSpace();
                if (currentPage != 0)
                {
                    if (GUILayout.Button(backButtonText, navButtonStyle))
                    {
                        if (currentPage != 0)
                        {
                            currentPage--;
                        }
                    }
                }

                string button2Text = "Next";
                if (currentPage == 0) button2Text = "Start";
                else if (currentPage == PAGES.End) button2Text = "Close";
                else button2Text = "Next";

                EditorGUILayout.Space();
                if (GUILayout.Button(button2Text, navButtonStyle))
                {
                    if (currentPage == PAGES.End)
                    {
                        this.Close();
                    }
                    currentPage++;
                }

                GUILayout.FlexibleSpace();
            }
        }
    }

    class SimpleTreeView : TreeView
    {
        public SimpleTreeView(TreeViewState state) : base(state)
        {
            Reload();
            Repaint();
            ExpandAll();
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override bool CanChangeExpandedState(TreeViewItem item)
        {
            return !(item is AudioSourceItem);
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);

            if (selectedIds.Count > 0)
            {
                var item = FindItem(selectedIds[0], rootItem);
                GameObject go = null;
                if (item.hasChildren)
                {
                    if (item is ParentItem)
                    {
                        go = ((ParentItem)item).gameObject;
                    }
                }
                else
                {
                    go = ((ParentItem)((AudioSourceItem)item).parent).gameObject;
                }
                Selection.activeGameObject = go;
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem (-1, -1);

            CreateItems(root, Resources.FindObjectsOfTypeAll<AudioSource>());
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            SetupDepthsFromParentsAndChildren(root);

            return root;
        }

        private class AudioSourceItem : TreeViewItem
        {
            const string audioIcon = "AudioSource Icon";
            public AudioSourceItem(AudioSource source) : base(source.GetHashCode())
            {
                displayName = (source.clip ? source.clip.name : "None");
                icon = (Texture2D)EditorGUIUtility.IconContent(audioIcon).image;
            }
        }

        private class ParentItem : TreeViewItem
        {
            public GameObject gameObject;
            const string goIcon = "GameObject Icon";
            const string prefabIcon = "Prefab Icon";
            const string prefabModelIcon = "PrefabModel Icon";
            const string prefabVariantIcon = "PrefabVariant Icon";

            public ParentItem(GameObject go) : base(go.GetHashCode(), 0, go.name)
            {
                gameObject = go;
                var foundAudio = gameObject.GetComponents<AudioSource>();
                for (int i = 0; i < foundAudio.Length; i++)
                {
                    AddChild(new AudioSourceItem(foundAudio[i]));
                }

#if UNITY_2018_3_OR_NEWER
                switch (PrefabUtility.GetPrefabAssetType(go))
                {
                    case PrefabAssetType.NotAPrefab:
                    icon = (Texture2D)EditorGUIUtility.IconContent(goIcon).image;
                        break;
                    case PrefabAssetType.Regular:
                    icon = (Texture2D)EditorGUIUtility.IconContent(prefabIcon).image;
                        break;
                    case PrefabAssetType.Model:
                    icon = (Texture2D)EditorGUIUtility.IconContent(prefabModelIcon).image;
                        break;
                    case PrefabAssetType.Variant:
                    icon = (Texture2D)EditorGUIUtility.IconContent(prefabVariantIcon).image;
                        break;
                }
#else
                switch (PrefabUtility.GetPrefabType(go))
                {
                    case PrefabType.None:
                        icon = (Texture2D)EditorGUIUtility.IconContent(goIcon).image;
                        break;
                    case PrefabType.Prefab:
                    case PrefabType.PrefabInstance:
                        icon = (Texture2D)EditorGUIUtility.IconContent(prefabIcon).image;
                        break;
                    case PrefabType.ModelPrefab:
                    case PrefabType.ModelPrefabInstance:
                        icon = (Texture2D)EditorGUIUtility.IconContent(prefabModelIcon).image;
                        break;
                }
#endif //UNITY_2018_3_OR_NEWER
            }
        }

        private class SceneItem : TreeViewItem
        {
            public Scene m_scene;
            const string sceneIcon = "SceneAsset Icon";
            const string folderIcon = "Folder Icon";

            public SceneItem(Scene scene) : base (scene.GetHashCode())
            {
                m_scene = scene;
                if (m_scene.IsValid())
                {
                    displayName = m_scene.name;
                    icon = (Texture2D)EditorGUIUtility.IconContent(sceneIcon).image;
                }
                else
                {
                    displayName = "Assets";
                    icon = (Texture2D)EditorGUIUtility.IconContent(folderIcon).image;
                }
            }
        }

        private void CreateItems(TreeViewItem root, AudioSource[] audioSources)
        {
            for(int i = 0; i < audioSources.Length; i++)
            {
                AudioSource audioSource = audioSources[i];

                GameObject obj = audioSource.gameObject;
                var sourceItem = FindItem(obj.GetHashCode(), root);
                if (sourceItem == null)
                {
                    List<GameObject> gameObjects = new List<GameObject>();
                    gameObjects.Add(obj);
                    while (obj.transform.parent != null)
                    {
                        obj = obj.transform.parent.gameObject;
                        gameObjects.Add(obj);
                    }
                    gameObjects.Reverse();

                    var parentItem = FindItem(obj.scene.GetHashCode(), root);
                    if (parentItem == null)
                    {
                        parentItem = new SceneItem(obj.scene);
                        root.AddChild(parentItem);
                    }

                    foreach (var go in gameObjects)
                    {
                        var objItem = FindItem(go.GetHashCode(), root);
                        if (objItem == null)
                        {
                            objItem = new ParentItem(go);
                            parentItem.AddChild(objItem);
                        }
                        parentItem = objItem;
                    }
                }
            }
        }

        const float BodyHeight = 200;

        public void Drawlayout()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, BodyHeight);
            rect = EditorGUI.IndentedRect(rect);

            OnGUI(rect);
            Toolbar();
        }

        public void Toolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var style = "miniButton";
                if (GUILayout.Button("Expand All", style))
                {
                    ExpandAll();
                }

                if (GUILayout.Button("Collapse All", style))
                {
                    CollapseAll();
                }
            }
        }
    }
}