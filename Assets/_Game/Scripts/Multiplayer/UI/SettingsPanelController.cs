using System;
using UnityEngine;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private InputField displayName;
        [SerializeField] private Dropdown turningMode;
        [SerializeField] private Slider snapAngle;
        [SerializeField] private Slider smoothSpeed;
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider effectsVolume;
        [SerializeField] private Text snapValue;
        [SerializeField] private Text smoothValue;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text effectsValue;
        [SerializeField] private Button[] colorButtons;
        [SerializeField] private Image colorPreview;
        [SerializeField] private Button backButton;
        [SerializeField] private Button snapModeButton;
        [SerializeField] private Button smoothModeButton;

        private LocalPlayerProfile editing;
        private Action onBack;

        public void Configure(InputField nameInput, Dropdown modeDropdown, Slider snapSlider, Slider smoothSlider, Slider masterSlider, Slider effectsSlider, Text snapLabel, Text smoothLabel, Text masterLabel, Text effectsLabel, Button[] paletteButtons, Image preview, Button back, Button snapButton, Button smoothButton)
        {
            displayName = nameInput; turningMode = modeDropdown; snapAngle = snapSlider; smoothSpeed = smoothSlider;
            masterVolume = masterSlider; effectsVolume = effectsSlider; snapValue = snapLabel; smoothValue = smoothLabel;
            masterValue = masterLabel; effectsValue = effectsLabel; colorButtons = paletteButtons; colorPreview = preview; backButton = back;
            snapModeButton = snapButton; smoothModeButton = smoothButton;
        }

        private void Awake()
        {
            backButton.onClick.AddListener(SaveAndBack);
            snapAngle.onValueChanged.AddListener(_ => RefreshLabels());
            smoothSpeed.onValueChanged.AddListener(_ => RefreshLabels());
            masterVolume.onValueChanged.AddListener(_ => RefreshLabels());
            effectsVolume.onValueChanged.AddListener(_ => RefreshLabels());
            snapModeButton.onClick.AddListener(() => turningMode.value = 0);
            smoothModeButton.onClick.AddListener(() => turningMode.value = 1);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                int captured = i;
                colorButtons[i].onClick.AddListener(() => SelectColor(captured));
            }
        }

        public void Open(Action backAction)
        {
            onBack = backAction;
            editing = GameBootstrap.Instance.Profile.Current.Clone();
            displayName.text = editing.DisplayName;
            turningMode.value = (int)editing.Turning;
            snapAngle.value = editing.SnapTurnAngle;
            smoothSpeed.value = editing.SmoothTurnSpeed;
            masterVolume.value = editing.MasterVolume;
            effectsVolume.value = editing.SoundEffectVolume;
            colorPreview.color = GameBootstrap.Instance.Profile.GetColor(editing.ColorIndex);
            RefreshLabels();
        }

        private void SaveAndBack()
        {
            if (editing == null) editing = new LocalPlayerProfile();
            editing.DisplayName = displayName.text;
            editing.Turning = turningMode.value == 1 ? TurningMode.Smooth : TurningMode.Snap;
            editing.SnapTurnAngle = snapAngle.value;
            editing.SmoothTurnSpeed = smoothSpeed.value;
            editing.MasterVolume = masterVolume.value;
            editing.SoundEffectVolume = effectsVolume.value;
            GameBootstrap.Instance.Profile.Save(editing);
            onBack?.Invoke();
        }

        private void SelectColor(int index)
        {
            if (editing == null) editing = GameBootstrap.Instance.Profile.Current.Clone();
            editing.ColorIndex = index;
            colorPreview.color = GameBootstrap.Instance.Profile.GetColor(index);
        }

        private void RefreshLabels()
        {
            if (snapValue != null) snapValue.text = $"{snapAngle.value:0} degrees";
            if (smoothValue != null) smoothValue.text = $"{smoothSpeed.value:0} deg/sec";
            if (masterValue != null) masterValue.text = $"{masterVolume.value * 100f:0}%";
            if (effectsValue != null) effectsValue.text = $"{effectsVolume.value * 100f:0}%";
        }
    }
}
