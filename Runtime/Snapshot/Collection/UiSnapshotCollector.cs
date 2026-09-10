#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace UniTestify
{
    /// <summary>UI 要素とゲーム状態を、その時点のシーン情報と合わせて観測文書へまとめます。</summary>
    internal static class UiSnapshotCollector
    {
        /// <summary>キャッシュを介さず指定フレームの観測を組み立てます。</summary>
        internal static UiSnapshotDocument CaptureUncached(int frameCount)
        {
            var selectedObject = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            var elements = UiSnapshotElementCollector.CollectElements(selectedObject);
            var gameEntries = UiSnapshotGameStateCollector.CollectGameEntries();

            return new UiSnapshotDocument
            {
                capturedAt = DateTimeOffset.Now.ToString("o"),
                frame = frameCount,
                activeScene = SceneManager.GetActiveScene().name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                focusedPath = selectedObject == null ? string.Empty : UiVisibilityUtility.BuildPath(selectedObject.transform),
                elements = elements.ToArray(),
                game = gameEntries.ToArray(),
            };
        }
    }
}
#endif
