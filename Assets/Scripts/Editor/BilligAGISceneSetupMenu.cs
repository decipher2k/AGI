using BilligAGI;
using BilligAGI.Bio;
using BilligAGI.Kern;
using BilligAGI.Sensorik;
using BilligAGI.UI;
using BilligAGI.Welt;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BilligAGI.Editor
{
    public static class BilligAGISceneSetupMenu
    {
        private const string RootMenu = "Tools/Billig-AGI/Scene Setup/";

        [MenuItem(RootMenu + "Create AGIConfig Asset", priority = 10)]
        public static void CreateConfigAsset()
        {
            EnsureFolder("Assets/Config");

            var existingGuids = AssetDatabase.FindAssets("t:AGIConfig");
            if (existingGuids.Length > 0)
            {
                var existingPath = AssetDatabase.GUIDToAssetPath(existingGuids[0]);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(existingPath);
                EditorUtility.DisplayDialog("AGIConfig", "Es existiert bereits eine AGIConfig. Vorhandene Asset wurde ausgewaehlt.", "OK");
                return;
            }

            var config = ScriptableObject.CreateInstance<AGIConfig>();
            const string assetPath = "Assets/Config/AGIConfig.asset";
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log("[SceneSetup] AGIConfig erstellt: Assets/Config/AGIConfig.asset");
        }

        [MenuItem(RootMenu + "Setup Complete Scene (with API Server)", priority = 11)]
        public static void SetupCompleteSceneWithApi()
        {
            SetupScene(includeApiServer: true);
        }

        [MenuItem(RootMenu + "Setup Core Scene (without API Server)", priority = 12)]
        public static void SetupCoreSceneWithoutApi()
        {
            SetupScene(includeApiServer: false);
        }

        [MenuItem(RootMenu + "Rebuild World Model From Scene", priority = 20)]
        public static void RebuildWorldModelFromScene()
        {
            var controllers = Object.FindObjectsByType<WeltController>(FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                EditorUtility.DisplayDialog("World Rebuild", "Kein WeltController in der Szene gefunden.", "OK");
                return;
            }

            var weltController = controllers[0];

            if (weltController.weltModell == null)
            {
                var kerne = Object.FindObjectsByType<AGIKern>(FindObjectsSortMode.None);
                if (kerne != null && kerne.Length > 0)
                    weltController.weltModell = kerne[0].GetWeltModell();

                if (weltController.weltModell == null)
                    weltController.weltModell = new WeltModell();
            }

            Transform root = GameObject.Find("World")?.transform;
            int count = weltController.RegistriereSzeneObjekte(root, clearVorher: true);

            EditorUtility.SetDirty(weltController);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[SceneSetup] Weltmodell aus Szene neu aufgebaut: {count} Objekte registriert.");
            EditorUtility.DisplayDialog("World Rebuild", $"Weltmodell aktualisiert. Registrierte Objekte: {count}", "OK");
        }

        [MenuItem(RootMenu + "Open README Setup Section", priority = 30)]
        public static void OpenReadme()
        {
            string readmePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "README.md"));
            if (!File.Exists(readmePath))
            {
                EditorUtility.DisplayDialog("README", "README.md konnte nicht gefunden werden.", "OK");
                return;
            }

            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(readmePath, 1);
        }

        private static void SetupScene(bool includeApiServer)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Billig-AGI Scene Setup");

            var config = FindOrCreateConfig();

            var directionalLight = FindOrCreateComponent<Light>("Directional Light");
            directionalLight.type = LightType.Directional;
            directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var wetterSystem = FindOrCreateComponent<WetterSystem>("Directional Light");
            wetterSystem.sonne = directionalLight;

            var worldRoot = FindOrCreate("World");
            var weltController = worldRoot.GetComponent<WeltController>();
            if (weltController == null) weltController = UndoAddComponent<WeltController>(worldRoot);
            weltController.wetterSystem = wetterSystem;
            weltController.directionalLight = directionalLight;
            worldRoot.tag = "Untagged";
            worldRoot.layer = LayerMask.NameToLayer("Default");

            var weltGenerator = worldRoot.GetComponent<WeltGenerator>();
            if (weltGenerator == null) weltGenerator = UndoAddComponent<WeltGenerator>(worldRoot);

            var terrainRoot = FindOrCreateChild(worldRoot, "TerrainRoot");
            terrainRoot.transform.localPosition = Vector3.zero;

            var agentRoot = FindOrCreate("AGI Agent");
            var sensorSuite = agentRoot.GetComponent<SensorSuite>();
            if (sensorSuite == null) sensorSuite = UndoAddComponent<SensorSuite>(agentRoot);

            var agiAgent = agentRoot.GetComponent<AGIAgent>();
            if (agiAgent == null) agiAgent = UndoAddComponent<AGIAgent>(agentRoot);

            var aktionsController = agentRoot.GetComponent<AktionsController>();
            if (aktionsController == null) aktionsController = UndoAddComponent<AktionsController>(agentRoot);

            var body = agentRoot.GetComponent<Rigidbody>();
            if (body == null) body = UndoAddComponent<Rigidbody>(agentRoot);
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var handAnchor = FindOrCreateChild(agentRoot, "HandAnchor").transform;
            handAnchor.localPosition = new Vector3(0.3f, 1.2f, 0.6f);
            agiAgent.handPunkt = handAnchor;

            var camGo = FindOrCreateChild(agentRoot, "Agent Camera");
            camGo.transform.localPosition = new Vector3(0f, 1.6f, -2.8f);
            camGo.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = UndoAddComponent<Camera>(camGo);
            cam.tag = "MainCamera";
            sensorSuite.agentCamera = cam;

            agiAgent.sensorSuite = sensorSuite;
            aktionsController.agent = agiAgent;
            aktionsController.sensorSuite = sensorSuite;

            var kernGo = FindOrCreate("AGI Core");
            var agiKern = kernGo.GetComponent<AGIKern>();
            if (agiKern == null) agiKern = UndoAddComponent<AGIKern>(kernGo);

            agiKern.config = config;
            agiKern.agent = agiAgent;
            agiKern.aktionsController = aktionsController;
            agiKern.sensorSuite = sensorSuite;
            agiKern.weltController = weltController;
            agiKern.weltGenerator = weltGenerator;
            agiKern.wetterSystem = wetterSystem;

            var autoTrainerGo = FindOrCreate("AGI AutoTrainer");
            var autoTrainer = autoTrainerGo.GetComponent<AutoTrainer>();
            if (autoTrainer == null) autoTrainer = UndoAddComponent<AutoTrainer>(autoTrainerGo);
            autoTrainer.agiKern = agiKern;

            if (includeApiServer)
            {
                var apiGo = FindOrCreate("AGI API Server");
                var apiServer = apiGo.GetComponent<AGIApiServer>();
                if (apiServer == null) apiServer = UndoAddComponent<AGIApiServer>(apiGo);
                apiServer.agiKern = agiKern;
            }

            var canvas = EnsureCanvas();
            var chatUi = SetupChatUi(canvas, agiKern, autoTrainer);
            SetupStatusOverlay(canvas, agiKern);
            SetupZielAnzeige(canvas, agiKern);

            EnsureEventSystem();
            Selection.activeObject = kernGo;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            string mode = includeApiServer ? "inkl. API-Server" : "ohne API-Server";
            Debug.Log($"[SceneSetup] Billig-AGI Scene Setup abgeschlossen ({mode}). ChatUI: {(chatUi != null ? "ok" : "fehlt")}.");
        }

        private static ChatUI SetupChatUi(Canvas canvas, AGIKern agiKern, AutoTrainer autoTrainer)
        {
            var root = FindOrCreateChild(canvas.gameObject, "ChatUI");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.02f, 0.02f);
            rect.anchorMax = new Vector2(0.55f, 0.55f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panelImage = root.GetComponent<Image>();
            if (panelImage == null) panelImage = UndoAddComponent<Image>(root);
            panelImage.color = new Color(0f, 0f, 0f, 0.45f);

            var scrollGo = FindOrCreateChild(root, "ScrollView");
            var scrollRect = EnsureScrollRect(scrollGo);
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0.02f, 0.2f);
            scrollRectTransform.anchorMax = new Vector2(0.98f, 0.98f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;

            var inputGo = FindOrCreateChild(root, "InputField");
            var inputField = EnsureInputField(inputGo);
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0.02f, 0.02f);
            inputRect.anchorMax = new Vector2(0.78f, 0.16f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            var buttonGo = FindOrCreateChild(root, "SendenButton");
            var button = EnsureButton(buttonGo, "Senden");
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.8f, 0.02f);
            buttonRect.anchorMax = new Vector2(0.98f, 0.16f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            var chatUi = root.GetComponent<ChatUI>();
            if (chatUi == null) chatUi = UndoAddComponent<ChatUI>(root);
            chatUi.inputField = inputField;
            chatUi.scrollRect = scrollRect;
            chatUi.chatText = scrollRect.content.GetComponentInChildren<Text>();
            chatUi.sendenButton = button;
            chatUi.agiKern = agiKern;
            chatUi.autoTrainer = autoTrainer;
            return chatUi;
        }

        private static void SetupStatusOverlay(Canvas canvas, AGIKern agiKern)
        {
            var root = FindOrCreateChild(canvas.gameObject, "StatusOverlay");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.58f, 0.52f);
            rect.anchorMax = new Vector2(0.98f, 0.98f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var panelImage = root.GetComponent<Image>();
            if (panelImage == null) panelImage = UndoAddComponent<Image>(root);
            panelImage.color = new Color(0f, 0f, 0f, 0.35f);

            var titleGo = FindOrCreateChild(root, "Title");
            var titleText = EnsureSimpleText(titleGo, "Status Overlay (F1)", 18, TextAnchor.MiddleLeft);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.86f);
            titleRect.anchorMax = new Vector2(0.96f, 0.98f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleText.color = Color.white;

            var phaseGo = FindOrCreateChild(root, "PhaseText");
            var phaseText = EnsureSimpleText(phaseGo, "Phase: -", 14, TextAnchor.MiddleLeft);
            var phaseRect = phaseGo.GetComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0.04f, 0.72f);
            phaseRect.anchorMax = new Vector2(0.96f, 0.84f);
            phaseRect.offsetMin = Vector2.zero;
            phaseRect.offsetMax = Vector2.zero;

            var modeGo = FindOrCreateChild(root, "ModeText");
            var modeText = EnsureSimpleText(modeGo, "Modus: -", 14, TextAnchor.MiddleLeft);
            var modeRect = modeGo.GetComponent<RectTransform>();
            modeRect.anchorMin = new Vector2(0.04f, 0.58f);
            modeRect.anchorMax = new Vector2(0.96f, 0.7f);
            modeRect.offsetMin = Vector2.zero;
            modeRect.offsetMax = Vector2.zero;

            var zielGo = FindOrCreateChild(root, "ZielText");
            var zielText = EnsureSimpleText(zielGo, "Ziel: -", 14, TextAnchor.MiddleLeft);
            var zielRect = zielGo.GetComponent<RectTransform>();
            zielRect.anchorMin = new Vector2(0.04f, 0.44f);
            zielRect.anchorMax = new Vector2(0.96f, 0.56f);
            zielRect.offsetMin = Vector2.zero;
            zielRect.offsetMax = Vector2.zero;

            var overlay = root.GetComponent<StatusOverlay>();
            if (overlay == null) overlay = UndoAddComponent<StatusOverlay>(root);
            overlay.agiKern = agiKern;
            overlay.overlayPanel = root;
            overlay.phaseText = phaseText;
            overlay.modusText = modeText;
            overlay.zielText = zielText;
        }

        private static void SetupZielAnzeige(Canvas canvas, AGIKern agiKern)
        {
            var root = FindOrCreateChild(canvas.gameObject, "ZielAnzeige");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.58f, 0.02f);
            rect.anchorMax = new Vector2(0.98f, 0.48f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = root.GetComponent<Image>();
            if (image == null) image = UndoAddComponent<Image>(root);
            image.color = new Color(0f, 0f, 0f, 0.35f);

            var listRoot = FindOrCreateChild(root, "Liste");
            var listRect = listRoot.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0.04f, 0.04f);
            listRect.anchorMax = new Vector2(0.96f, 0.96f);
            listRect.offsetMin = Vector2.zero;
            listRect.offsetMax = Vector2.zero;

            var vertical = listRoot.GetComponent<VerticalLayoutGroup>();
            if (vertical == null) vertical = UndoAddComponent<VerticalLayoutGroup>(listRoot);
            vertical.childControlHeight = true;
            vertical.childForceExpandHeight = false;
            vertical.childControlWidth = true;
            vertical.childForceExpandWidth = true;
            vertical.spacing = 4f;

            var fitter = listRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = UndoAddComponent<ContentSizeFitter>(listRoot);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var itemTemplate = FindOrCreateChild(root, "ZielEintragTemplate");
            itemTemplate.SetActive(false);
            var templateRect = itemTemplate.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(1f, 1f);
            templateRect.sizeDelta = new Vector2(0f, 24f);

            var itemText = EnsureSimpleText(itemTemplate, "Template", 14, TextAnchor.MiddleLeft);
            var itemTextRect = itemTemplate.GetComponent<RectTransform>();
            itemTextRect.offsetMin = new Vector2(8f, 0f);
            itemTextRect.offsetMax = new Vector2(-8f, 0f);
            itemText.color = Color.white;

            var zielAnzeige = root.GetComponent<ZielAnzeige>();
            if (zielAnzeige == null) zielAnzeige = UndoAddComponent<ZielAnzeige>(root);
            zielAnzeige.agiKern = agiKern;
            zielAnzeige.zielListeParent = listRoot.transform;
            zielAnzeige.zielEintragPrefab = itemTemplate;
        }

        private static Canvas EnsureCanvas()
        {
            var canvasGo = FindOrCreate("Canvas");
            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = UndoAddComponent<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = UndoAddComponent<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                UndoAddComponent<GraphicRaycaster>(canvasGo);

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            UndoAddComponent<EventSystem>(eventSystem);
            UndoAddComponent<StandaloneInputModule>(eventSystem);
        }

        private static ScrollRect EnsureScrollRect(GameObject root)
        {
            var image = root.GetComponent<Image>();
            if (image == null) image = UndoAddComponent<Image>(root);
            image.color = new Color(0f, 0f, 0f, 0.2f);

            var scroll = root.GetComponent<ScrollRect>();
            if (scroll == null) scroll = UndoAddComponent<ScrollRect>(root);
            scroll.horizontal = false;

            var viewport = FindOrCreateChild(root, "Viewport");
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImage = viewport.GetComponent<Image>();
            if (viewportImage == null) viewportImage = UndoAddComponent<Image>(viewport);
            viewportImage.color = new Color(0f, 0f, 0f, 0f);

            if (viewport.GetComponent<Mask>() == null)
                UndoAddComponent<Mask>(viewport).showMaskGraphic = false;

            var content = FindOrCreateChild(viewport, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(8f, -600f);
            contentRect.offsetMax = new Vector2(-8f, -8f);

            var text = content.GetComponent<Text>();
            if (text == null) text = UndoAddComponent<Text>(content);
            text.font = GetDefaultFont();
            text.fontSize = 16;
            text.supportRichText = true;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "";
            text.color = Color.white;

            var contentFitter = content.GetComponent<ContentSizeFitter>();
            if (contentFitter == null) contentFitter = UndoAddComponent<ContentSizeFitter>(content);
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return scroll;
        }

        private static InputField EnsureInputField(GameObject root)
        {
            var image = root.GetComponent<Image>();
            if (image == null) image = UndoAddComponent<Image>(root);
            image.color = new Color(1f, 1f, 1f, 0.1f);

            var input = root.GetComponent<InputField>();
            if (input == null) input = UndoAddComponent<InputField>(root);

            var textGo = FindOrCreateChild(root, "Text");
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);

            var text = textGo.GetComponent<Text>();
            if (text == null) text = UndoAddComponent<Text>(textGo);
            text.font = GetDefaultFont();
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;

            var placeholderGo = FindOrCreateChild(root, "Placeholder");
            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 6f);
            placeholderRect.offsetMax = new Vector2(-10f, -6f);

            var placeholder = placeholderGo.GetComponent<Text>();
            if (placeholder == null) placeholder = UndoAddComponent<Text>(placeholderGo);
            placeholder.font = GetDefaultFont();
            placeholder.fontSize = 16;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "Nachricht oder /befehl...";
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            placeholder.fontStyle = FontStyle.Italic;

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Button EnsureButton(GameObject root, string label)
        {
            var image = root.GetComponent<Image>();
            if (image == null) image = UndoAddComponent<Image>(root);
            image.color = new Color(0.15f, 0.45f, 0.8f, 0.85f);

            var button = root.GetComponent<Button>();
            if (button == null) button = UndoAddComponent<Button>(root);

            var labelGo = FindOrCreateChild(root, "Text");
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = EnsureSimpleText(labelGo, label, 16, TextAnchor.MiddleCenter);
            text.color = Color.white;
            return button;
        }

        private static Text EnsureSimpleText(GameObject root, string content, int size, TextAnchor anchor)
        {
            var text = root.GetComponent<Text>();
            if (text == null) text = UndoAddComponent<Text>(root);
            text.font = GetDefaultFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        private static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static AGIConfig FindOrCreateConfig()
        {
            var guids = AssetDatabase.FindAssets("t:AGIConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var found = AssetDatabase.LoadAssetAtPath<AGIConfig>(path);
                if (found != null) return found;
            }

            CreateConfigAsset();
            guids = AssetDatabase.FindAssets("t:AGIConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<AGIConfig>(path);
            }

            throw new System.InvalidOperationException("AGIConfig konnte nicht erstellt werden.");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var split = folder.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);
                current = next;
            }
        }

        private static GameObject FindOrCreate(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) return go;

            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            var child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            bool uiParent = parent.GetComponent<RectTransform>() != null;
            var go = uiParent
                ? new GameObject(name, typeof(RectTransform))
                : new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, parent.transform, "Parent " + name);
            return go;
        }

        private static T FindOrCreateComponent<T>(string gameObjectName) where T : Component
        {
            var go = FindOrCreate(gameObjectName);
            var component = go.GetComponent<T>();
            if (component == null) component = UndoAddComponent<T>(go);
            return component;
        }

        private static T UndoAddComponent<T>(GameObject go) where T : Component
        {
            return (T)Undo.AddComponent(go, typeof(T));
        }
    }
}