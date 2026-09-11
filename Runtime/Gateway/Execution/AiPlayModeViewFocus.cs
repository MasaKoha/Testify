#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniTestify
{
    /// <summary>Editor への依存を持たず、撮影対象のフォーカスと解像度の安定待ちを仲介します。</summary>
    internal static class AiPlayModeViewFocus
    {
        private const int MaximumResolutionWaitFrames = 5;
        private const int RequiredStableResolutionFrames = 2;

        /// <summary>Editor 側が登録するフォーカス処理です。対象を利用できない場合は false を返します。</summary>
        internal static Func<string, bool> FocusHandler;

        /// <summary>撮影対象の誤記をフォーカスや観測より前に拒否します。</summary>
        internal static void Validate(string view)
        {
            if (!string.IsNullOrEmpty(view) && view != "game" && view != "simulator")
            {
                throw new ArgumentException("view は空文字列、game、simulator のいずれかを指定してください。", nameof(view));
            }
        }

        /// <summary>未指定または Editor の仲介処理が無い場合は、従来の撮影対象を維持します。</summary>
        internal static bool TryFocus(string view)
        {
            Validate(view);
            if (string.IsNullOrEmpty(view) || FocusHandler == null)
            {
                return false;
            }

            return FocusHandler(view);
        }

        /// <summary>フォーカス成功後、画面寸法が連続して安定するまで最大 5 フレーム待ちます。</summary>
        internal static IEnumerator<object> WaitForStableResolutionAsync()
        {
            var previousWidth = Screen.width;
            var previousHeight = Screen.height;
            var stableFrames = 0;
            // perf: 列挙子は view 指定の要求ごとにだけ生成し、フレームごとは値の比較だけにする。
            for (var waitedFrames = 0; waitedFrames < MaximumResolutionWaitFrames; waitedFrames++)
            {
                yield return null;
                var currentWidth = Screen.width;
                var currentHeight = Screen.height;
                stableFrames = currentWidth == previousWidth && currentHeight == previousHeight ? stableFrames + 1 : 0;
                // Editor のフォーカス反映が遅れても、最初の 1 フレームだけで安定と判断しない。
                if (stableFrames >= RequiredStableResolutionFrames)
                {
                    yield break;
                }

                previousWidth = currentWidth;
                previousHeight = currentHeight;
            }
        }
    }
}
#endif
