#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GorillaLocomotion;
using TheBestMonkeyGame.Multiplayer;
using TheBestMonkeyGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEditor;

namespace TheBestMonkeyGame.Editor
{
    public static class InGameMenuMilestoneBuilder
    {
        public const string MenuPrefabPath = "Assets/_Game/Prefabs/UI/InGameMenu.prefab";
        public const string InputAssetPath = "Assets/_Game/Input/TBMGInputActions.asset";
        public const string MenuActionReferencePath = "Assets/_Game/Input/LeftMenuButtonAction.asset";
        public const string MenuActionPath = "XRI LeftHand/Menu";
        public const string MenuBindingPath = "<XRController>{LeftHand}/menuButton";

        private const string VrPlayerPath = "Assets/_Game/Prefabs/VRPlayer.prefab";
        private const string NetworkPlayerPath = "Assets/_Game/Prefabs/Multiplayer/NetworkVRPlayer.prefab";
        private const string RayMaterialPath = "Assets/_Game/Materials/Multiplayer/UIAccent.mat";
        private const int UiLayer = 5;

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static readonly Color Navy = new(0.025f, 0.055f, 0.095f, 0.985f);
        private static readonly Color Panel = new(0.055f, 0.105f, 0.16f, 1f);
        private static readonly Color Accent = new(0.12f, 0.72f, 0.93f, 1f);
        private static readonly Color Ink = new(0.9f, 0.97f, 1f, 1f);

        [MenuItem("Tools/The Best Monkey Game/In-Game Menu/Build")]
        public static void Build()
        {
            try
            {
                EnsureFolder("Assets/_Game/Input");
                EnsureFolder("Assets/_Game/Prefabs/UI");
                InputActionReference menuAction = BuildInputAction();
                Material rayMaterial = RequireAsset<Material>(RayMaterialPath);
                GameObject menuPrefab = BuildMenuPrefab(menuAction, rayMaterial);
                AttachToPlayerPrefab(menuPrefab);
                NormalizeNetworkPlayerPrefab();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Validate();
                Debug.Log("IN_GAME_MENU_BUILD_SUCCESS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/The Best Monkey Game/In-Game Menu/Validate")]
        public static void Validate()
        {
            InputActionAsset input = RequireAsset<InputActionAsset>(InputAssetPath);
            InputAction action = input.FindAction(MenuActionPath, true);
            if (!action.bindings.Any(binding => binding.path == MenuBindingPath))
                throw new InvalidOperationException($"{MenuActionPath} is missing {MenuBindingPath}.");

            InputActionReference reference = RequireAsset<InputActionReference>(MenuActionReferencePath);
            if (reference.action == null || reference.action.id != action.id)
                throw new InvalidOperationException("The in-game menu does not reference the configured left menu action.");

            GameObject menu = RequireAsset<GameObject>(MenuPrefabPath);
            if (menu.GetComponent<InGameMenuController>() == null || menu.GetComponentsInChildren<Canvas>(true).Length != 1)
                throw new InvalidOperationException("InGameMenu prefab is incomplete.");

            GameObject player = RequireAsset<GameObject>(VrPlayerPath);
            if (player.GetComponentsInChildren<InGameMenuController>(true).Length != 1)
                throw new InvalidOperationException("VRPlayer must contain exactly one reusable in-game menu.");
            if (player.GetComponents<VRTurningController>().Length != 1)
                throw new InvalidOperationException("VRPlayer must contain exactly one turning controller.");

            GameObject networkPlayer = RequireAsset<GameObject>(NetworkPlayerPath);
            Transform localRoot = networkPlayer.transform.Find("LocalPlayerRoot");
            if (localRoot == null || localRoot.GetComponents<VRTurningController>().Length != 1 ||
                localRoot.GetComponentsInChildren<InGameMenuController>(true).Length != 1)
                throw new InvalidOperationException("NetworkVRPlayer local owner rig has an invalid menu or turning setup.");

            if (Time.timeScale != 1f) throw new InvalidOperationException("The in-game menu builder must never alter Time.timeScale.");
            Debug.Log($"IN_GAME_MENU_VALIDATION_SUCCESS action={MenuActionPath} binding={MenuBindingPath} singlePlayer=true multiplayerOwner=true timeScaleUnchanged=true");
        }

        private static InputActionReference BuildInputAction()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<InputActionAsset>();
                asset.name = "TBMGInputActions";
                AssetDatabase.CreateAsset(asset, InputAssetPath);
            }

            InputActionMap map = asset.FindActionMap("XRI LeftHand");
            if (map == null)
            {
                map = new InputActionMap("XRI LeftHand");
                asset.AddActionMap(map);
            }
            InputAction action = map.FindAction("Menu");
            if (action == null) action = map.AddAction("Menu", InputActionType.Button, expectedControlLayout: "Button");
            if (!action.bindings.Any(binding => binding.path == MenuBindingPath)) action.AddBinding(MenuBindingPath);
            EditorUtility.SetDirty(asset);

            InputActionReference reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(MenuActionReferencePath);
            if (reference == null)
            {
                reference = ScriptableObject.CreateInstance<InputActionReference>();
                AssetDatabase.CreateAsset(reference, MenuActionReferencePath);
            }
            reference.Set(action);
            reference.name = "XRI LeftHand Menu";
            EditorUtility.SetDirty(reference);
            AssetDatabase.SaveAssets();
            return reference;
        }

        private static GameObject BuildMenuPrefab(InputActionReference menuAction, Material rayMaterial)
        {
            GameObject root = new("InGameMenu");
            InGameMenuController controller = root.AddComponent<InGameMenuController>();

            GameObject canvasObject = new("InGameMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            canvasObject.layer = UiLayer;
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(760f, 760f);
            canvasRect.localScale = Vector3.one * 0.0018f;
            canvasObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            canvasObject.GetComponent<Image>().color = Navy;

            GameObject home = CreatePanel(canvasObject.transform, "HomePanel");
            CreateText(home.transform, "ROOM MENU", new Vector2(0f, 275f), new Vector2(680f, 75f), 43, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            CreateText(home.transform, "LOCAL PLAYER CONTROLS", new Vector2(0f, 225f), new Vector2(680f, 38f), 17, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            Button resume = CreateButton(home.transform, "RESUME", new Vector2(0f, 105f), new Vector2(410f, 68f));
            Button settings = CreateButton(home.transform, "SETTINGS", new Vector2(0f, 15f), new Vector2(410f, 68f));
            Button leave = CreateButton(home.transform, "LEAVE GAME", new Vector2(0f, -75f), new Vector2(410f, 68f));
            Text status = CreateText(home.transform, string.Empty, new Vector2(0f, -170f), new Vector2(650f, 60f), 19, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            CreateText(home.transform, "Head and controller tracking stay active while movement is suspended.", new Vector2(0f, -255f), new Vector2(650f, 70f), 16, TextAnchor.MiddleCenter, new Color(0.64f, 0.78f, 0.84f));

            GameObject settingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel");
            SettingsPanelController settingsController = settingsPanel.AddComponent<SettingsPanelController>();
            CreateText(settingsPanel.transform, "PLAYER SETTINGS", new Vector2(0f, 320f), new Vector2(690f, 58f), 35, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            InputField nameInput = CreateInput(settingsPanel.transform, "DisplayName", "PLAYER NAME", new Vector2(0f, 260f), new Vector2(430f, 52f), PlayerProfileService.MaxDisplayNameLength);
            Dropdown modeData = CreateHiddenTurningDropdown(settingsPanel.transform);
            Button snapMode = CreateButton(settingsPanel.transform, "SNAP TURN", new Vector2(-118f, 195f), new Vector2(215f, 48f));
            Button smoothMode = CreateButton(settingsPanel.transform, "SMOOTH TURN", new Vector2(118f, 195f), new Vector2(215f, 48f));
            Slider snap = CreateSlider(settingsPanel.transform, "SnapAngle", "SNAP ANGLE", 15f, 90f, new Vector2(20f, 130f), out Text snapValue);
            Slider smooth = CreateSlider(settingsPanel.transform, "SmoothSpeed", "SMOOTH SPEED", 30f, 180f, new Vector2(20f, 67f), out Text smoothValue);
            Slider master = CreateSlider(settingsPanel.transform, "MasterVolume", "MASTER VOLUME", 0f, 1f, new Vector2(20f, 4f), out Text masterValue);
            Slider effects = CreateSlider(settingsPanel.transform, "EffectsVolume", "SFX VOLUME", 0f, 1f, new Vector2(20f, -59f), out Text effectsValue);
            CreateText(settingsPanel.transform, "PLAYER COLOR", new Vector2(-250f, -125f), new Vector2(200f, 32f), 15, TextAnchor.MiddleLeft, new Color(0.63f, 0.8f, 0.87f), FontStyle.Bold);
            Image preview = CreateImage(settingsPanel.transform, "ColorPreview", new Vector2(265f, -125f), new Vector2(48f, 48f), Color.white);
            Button[] palette = new Button[6];
            for (int i = 0; i < palette.Length; i++)
                palette[i] = CreateButton(settingsPanel.transform, (i + 1).ToString(), new Vector2(-190f + i * 75f, -180f), new Vector2(58f, 46f));
            Button back = CreateButton(settingsPanel.transform, "SAVE & BACK", new Vector2(0f, -275f), new Vector2(350f, 56f));
            settingsController.Configure(nameInput, modeData, snap, smooth, master, effects, snapValue, smoothValue, masterValue, effectsValue, palette, preview, back, snapMode, smoothMode);

            settingsPanel.SetActive(false);
            canvasObject.SetActive(false);
            controller.Configure(menuAction, canvasObject, home, settingsPanel, resume, settings, leave, status, settingsController, rayMaterial);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static void AttachToPlayerPrefab(GameObject menuPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(VrPlayerPath);
            try
            {
                Transform existing = root.transform.Find("InGameMenu");
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, root.transform);
                menu.name = "InGameMenu";
                menu.transform.localPosition = Vector3.zero;
                menu.transform.localRotation = Quaternion.identity;
                menu.transform.localScale = Vector3.one;

                Transform head = RequireChildRecursive(root.transform, "Main Camera");
                VRTurningController turning = root.GetComponent<VRTurningController>();
                if (turning == null) turning = root.AddComponent<VRTurningController>();
                turning.Configure(root.transform, head);
                PrefabUtility.SaveAsPrefabAsset(root, VrPlayerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void NormalizeNetworkPlayerPrefab()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject root = PrefabUtility.LoadPrefabContents(NetworkPlayerPath);
            try
            {
                Transform local = root.transform.Find("LocalPlayerRoot");
                if (local == null) throw new InvalidOperationException("NetworkVRPlayer is missing LocalPlayerRoot.");
                VRTurningController[] turners = local.GetComponents<VRTurningController>();
                VRTurningController keep = turners.FirstOrDefault(component => PrefabUtility.GetCorrespondingObjectFromSource(component) != null) ?? turners.FirstOrDefault();
                foreach (VRTurningController turning in turners)
                    if (turning != keep) UnityEngine.Object.DestroyImmediate(turning);
                if (keep == null) keep = local.gameObject.AddComponent<VRTurningController>();
                keep.Configure(local, RequireChildRecursive(local, "Main Camera"));
                PrefabUtility.SaveAsPrefabAsset(root, NetworkPlayerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new(name, typeof(RectTransform));
            panel.layer = UiLayer;
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panel;
        }

        private static Text CreateText(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject item = new("Text", typeof(RectTransform), typeof(Text));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Text text = item.GetComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.fontStyle = style;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject item = new(label.Replace(" ", string.Empty) + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Button button = item.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Panel;
            colors.highlightedColor = new Color(0.08f, 0.34f, 0.46f, 1f);
            colors.pressedColor = Accent;
            colors.selectedColor = new Color(0.07f, 0.28f, 0.38f, 1f);
            colors.disabledColor = new Color(0.1f, 0.14f, 0.17f, 0.7f);
            button.colors = colors;
            item.GetComponent<Image>().color = Panel;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(button);
            CreateText(item.transform, label, Vector2.zero, size, 19, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            return button;
        }

        private static InputField CreateInput(Transform parent, string name, string placeholderValue, Vector2 position, Vector2 size, int characterLimit)
        {
            GameObject item = new(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            item.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.055f, 1f);
            Text text = CreateText(item.transform, string.Empty, Vector2.zero, new Vector2(size.x - 30f, size.y - 6f), 20, TextAnchor.MiddleCenter, Ink);
            Text placeholder = CreateText(item.transform, placeholderValue, Vector2.zero, new Vector2(size.x - 30f, size.y - 6f), 18, TextAnchor.MiddleCenter, new Color(0.35f, 0.55f, 0.64f));
            InputField input = item.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(input);
            return input;
        }

        private static Slider CreateSlider(Transform parent, string name, string label, float min, float max, Vector2 position, out Text valueText)
        {
            CreateText(parent, label, new Vector2(-255f, position.y), new Vector2(210f, 36f), 14, TextAnchor.MiddleLeft, new Color(0.63f, 0.8f, 0.87f), FontStyle.Bold);
            GameObject item = new(name, typeof(RectTransform), typeof(Slider), typeof(BoxCollider), typeof(VRRayTarget));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(330f, 38f);
            rect.anchoredPosition = position;
            CreateImage(item.transform, "Background", Vector2.zero, new Vector2(330f, 10f), new Color(0.08f, 0.18f, 0.22f));
            Image fill = CreateImage(item.transform, "Fill", new Vector2(-165f, 0f), new Vector2(330f, 10f), Accent);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            Image handle = CreateImage(item.transform, "Handle", Vector2.zero, new Vector2(25f, 25f), Ink);
            Slider slider = item.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            BoxCollider collider = item.GetComponent<BoxCollider>();
            collider.size = new Vector3(350f, 44f, 18f);
            collider.isTrigger = true;
            item.GetComponent<VRRayTarget>().Configure(slider);
            valueText = CreateText(parent, string.Empty, new Vector2(280f, position.y), new Vector2(145f, 36f), 14, TextAnchor.MiddleRight, Ink);
            return slider;
        }

        private static Dropdown CreateHiddenTurningDropdown(Transform parent)
        {
            GameObject item = new("TurningModeData", typeof(RectTransform), typeof(Dropdown));
            item.transform.SetParent(parent, false);
            Dropdown dropdown = item.GetComponent<Dropdown>();
            dropdown.options = new List<Dropdown.OptionData> { new("Snap"), new("Smooth") };
            item.SetActive(false);
            return dropdown;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject item = new(name, typeof(RectTransform), typeof(Image));
            item.layer = UiLayer;
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = item.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Transform RequireChildRecursive(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
            return result != null ? result : throw new InvalidOperationException($"{root.name} is missing {name}.");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : throw new InvalidOperationException($"Missing required asset: {path}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
#endif
