using UnityEngine;
using UnityEngine.UI;

namespace TheBestMonkeyGame.Multiplayer
{
    public sealed class VRRayTarget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private InputField inputField;
        [SerializeField] private Slider slider;
        private TouchScreenKeyboard keyboard;

        public void Configure(Button targetButton) { button = targetButton; inputField = null; slider = null; }
        public void Configure(InputField targetInput) { inputField = targetInput; button = null; slider = null; }
        public void Configure(Slider targetSlider) { slider = targetSlider; inputField = null; button = null; }

        public void SetHovered(bool hovered)
        {
            Selectable selectable = button != null ? button : inputField != null ? inputField : slider;
            if (selectable == null || !selectable.IsInteractable()) return;
            if (hovered) selectable.Select();
        }

        public void Trigger(Vector3 worldHit)
        {
            if (button != null && button.IsInteractable())
            {
                button.onClick.Invoke();
                return;
            }
            if (slider != null && slider.IsInteractable())
            {
                RectTransform rect = slider.transform as RectTransform;
                Vector3 local = rect.InverseTransformPoint(worldHit);
                slider.normalizedValue = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x);
                return;
            }
            if (inputField == null || !inputField.IsInteractable()) return;
            inputField.Select();
            inputField.ActivateInputField();
#if UNITY_ANDROID && !UNITY_EDITOR
            keyboard = TouchScreenKeyboard.Open(inputField.text, TouchScreenKeyboardType.Default, false, false, false, false, inputField.placeholder is Text text ? text.text : string.Empty, inputField.characterLimit);
#endif
        }

        private void Update()
        {
            if (keyboard == null || inputField == null) return;
            inputField.text = keyboard.text;
            if (keyboard.status != TouchScreenKeyboard.Status.Visible) keyboard = null;
        }
    }
}
