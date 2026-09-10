#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniTestify
{
    /// <summary>UI コンポーネントの意味と可視性を観測要素へ変換します。</summary>
    internal static class UiSnapshotElementFactory
    {
        private const string ButtonKind = "Button";
        private const string ToggleKind = "Toggle";
        private const string SliderKind = "Slider";
        private const string InputKind = "Input";
        private const string SelectableKind = "Selectable";
        private const string TextKind = "Text";
        private const int SelectableLabelLength = 80;
        private const float MinimumScreenVisibleRatio = 0.1f;
        private const float MinimumMaskVisibleRatio = 0.5f;
        private const int TextLabelLength = 120;

        /// <summary>操作対象の意味と可視性を取得できた場合に観測要素を返します。</summary>
        internal static bool TryCreateSelectableElement(Selectable selectable, GameObject selectedObject, out UiSnapshotElement element)
        {
            element = null;
            var rectTransform = selectable.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            if (!UiVisibilityUtility.TryGetScreenRect(rectTransform, out var rectValues))
            {
                return false;
            }

            var inputField = selectable as TMP_InputField;
            var toggle = selectable as Toggle;
            var slider = selectable as Slider;
            var button = selectable as Button;
            var label = inputField == null
                ? UiVisibilityUtility.FindSelectableLabel(selectable.gameObject, SelectableLabelLength)
                : GetInputPlaceholderLabel(inputField);

            element = new UiSnapshotElement
            {
                path = UiVisibilityUtility.BuildPath(selectable.transform),
                name = selectable.name,
                kind = ResolveSelectableKind(button, toggle, slider, inputField),
                label = label,
                rect = rectValues,
                offscreen = UiVisibilityUtility.ComputeVisibleRatio(rectValues, new[] { 0f, 0f, (float)Screen.width, Screen.height }) < MinimumScreenVisibleRatio,
                clipped = IsClipped(rectTransform, rectValues),
                interactable = UiVisibilityUtility.IsInteractable(selectable),
                blockedBy = GetBlockingObjectName(selectable.gameObject),
                focused = selectedObject == selectable.gameObject,
                value = ResolveSelectableValue(toggle, slider, inputField),
            };
            return true;
        }

        /// <summary>独立したテキストの意味と可視性を取得できた場合に観測要素を返します。</summary>
        internal static bool TryCreateTextElement(TextMeshProUGUI textObject, GameObject selectedObject, out UiSnapshotElement element)
        {
            element = null;
            var rectTransform = textObject.transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            if (!UiVisibilityUtility.TryGetScreenRect(rectTransform, out var rectValues))
            {
                return false;
            }

            element = new UiSnapshotElement
            {
                path = UiVisibilityUtility.BuildPath(textObject.transform),
                name = textObject.name,
                kind = TextKind,
                label = UiVisibilityUtility.Truncate(textObject.text, TextLabelLength),
                rect = rectValues,
                offscreen = UiVisibilityUtility.ComputeVisibleRatio(rectValues, new[] { 0f, 0f, (float)Screen.width, Screen.height }) < MinimumScreenVisibleRatio,
                clipped = IsClipped(rectTransform, rectValues),
                interactable = false,
                blockedBy = GetTextBlockingObjectName(textObject.gameObject),
                focused = selectedObject == textObject.gameObject,
                value = string.Empty,
            };
            return true;
        }

        private static bool IsClipped(RectTransform elementTransform, float[] elementRect)
        {
            // perf: 観測時のみ祖先を探索し、毎フレームの階層検索を避ける。
            for (var ancestor = elementTransform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var rectangleMask = ancestor.GetComponent<RectMask2D>();
                var mask = ancestor.GetComponent<Mask>();
                var hasRectangleMask = rectangleMask != null && rectangleMask.isActiveAndEnabled;
                var hasImageMask = mask != null && mask.isActiveAndEnabled && ancestor.GetComponent<Image>() != null;
                if (!hasRectangleMask && !hasImageMask)
                {
                    continue;
                }

                return UiVisibilityUtility.TryGetScreenRect(ancestor as RectTransform, out var clipRect)
                    && UiVisibilityUtility.ComputeVisibleRatio(elementRect, clipRect) < MinimumMaskVisibleRatio;
            }

            return false;
        }

        private static string ResolveSelectableKind(Button button, Toggle toggle, Slider slider, TMP_InputField inputField)
        {
            if (button != null)
            {
                return ButtonKind;
            }

            if (toggle != null)
            {
                return ToggleKind;
            }

            if (slider != null)
            {
                return SliderKind;
            }

            if (inputField != null)
            {
                return InputKind;
            }

            return SelectableKind;
        }

        private static string ResolveSelectableValue(Toggle toggle, Slider slider, TMP_InputField inputField)
        {
            if (toggle != null)
            {
                return toggle.isOn ? "on" : "off";
            }

            if (slider != null)
            {
                return slider.value.ToString(CultureInfo.InvariantCulture);
            }

            if (inputField != null)
            {
                return inputField.text ?? string.Empty;
            }

            return string.Empty;
        }

        private static string GetInputPlaceholderLabel(TMP_InputField inputField)
        {
            if (inputField == null || inputField.placeholder == null)
            {
                return string.Empty;
            }

            var textObject = inputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (textObject == null)
            {
                return string.Empty;
            }

            return UiVisibilityUtility.Truncate(textObject.text, SelectableLabelLength);
        }

        private static string GetTextBlockingObjectName(GameObject target)
        {
            var blockingObject = UiVisibilityUtility.FindTextBlockingObject(target);
            return blockingObject == null ? string.Empty : blockingObject.name;
        }

        private static string GetBlockingObjectName(GameObject target)
        {
            var blockingObject = UiVisibilityUtility.FindBlockingObject(target);
            return blockingObject == null ? string.Empty : blockingObject.name;
        }
    }
}
#endif
