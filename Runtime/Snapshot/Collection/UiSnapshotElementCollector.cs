#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniTestify
{
    /// <summary>観測対象の UI を探索し、画面上の順序で要素を並べます。</summary>
    internal static class UiSnapshotElementCollector
    {
        /// <summary>画面順に並んだ UI 観測要素を返します。</summary>
        internal static List<UiSnapshotElement> CollectElements(GameObject selectedObject)
        {
            var elements = new List<UiSnapshotElement>();
            var selectableObjects = UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
            for (var selectableIndex = 0; selectableIndex < selectableObjects.Length; selectableIndex++)
            {
                var selectable = selectableObjects[selectableIndex];
                if (selectable == null || !UiVisibilityUtility.IsVisibleGraphicObject(selectable.gameObject))
                {
                    continue;
                }

                if (!UiSnapshotElementFactory.TryCreateSelectableElement(selectable, selectedObject, out var element))
                {
                    continue;
                }

                elements.Add(element);
            }

            var textObjects = UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
            for (var textIndex = 0; textIndex < textObjects.Length; textIndex++)
            {
                var textObject = textObjects[textIndex];
                if (textObject == null || !UiVisibilityUtility.IsVisibleGraphicObject(textObject.gameObject))
                {
                    continue;
                }

                if (textObject.GetComponentInParent<Selectable>() != null)
                {
                    continue;
                }

                if (!UiSnapshotElementFactory.TryCreateTextElement(textObject, selectedObject, out var element))
                {
                    continue;
                }

                elements.Add(element);
            }

            elements.Sort(CompareElements);
            return elements;
        }

        private static int CompareElements(UiSnapshotElement left, UiSnapshotElement right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftY = left.rect == null || left.rect.Length < 2 ? 0f : left.rect[1];
            var rightY = right.rect == null || right.rect.Length < 2 ? 0f : right.rect[1];
            var yComparison = -leftY.CompareTo(rightY);
            if (yComparison != 0)
            {
                return yComparison;
            }

            var leftX = left.rect == null || left.rect.Length < 1 ? 0f : left.rect[0];
            var rightX = right.rect == null || right.rect.Length < 1 ? 0f : right.rect[0];
            var xComparison = leftX.CompareTo(rightX);
            if (xComparison != 0)
            {
                return xComparison;
            }

            return string.Compare(left.path, right.path, StringComparison.Ordinal);
        }
    }
}
#endif
