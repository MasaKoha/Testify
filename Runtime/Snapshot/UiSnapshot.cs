#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniTestify
{
    /// <summary>同一フレームの UI 観測を共有し、収集・差分・出力への公開入口を維持します。</summary>
    public static class UiSnapshot
    {
        private static UiSnapshotDocument _cachedDocument;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _cachedDocument = null;
        }

        /// <summary>
        /// 現在フレーム内で完結する UI 状態を収集します。
        /// フレームをまたがる待機を入れないことで観測がゲームの見え方を変えないようにします。
        /// </summary>
        public static UiSnapshotDocument Capture()
        {
            if (Application.isPlaying)
            {
                return Capture(Time.frameCount);
            }

            ResetCache();
            return UiSnapshotCollector.CaptureUncached(Time.frameCount);
        }

        /// <summary>指定フレームの観測を共有し、フレーム変更時に収集し直します。</summary>
        internal static UiSnapshotDocument Capture(int frameCount)
        {
            // perf: 観測・準備待ち・差分計算で 1 フレームに複数回呼ばれるため。
            if (_cachedDocument == null || _cachedDocument.frame != frameCount)
            {
                _cachedDocument = UiSnapshotCollector.CaptureUncached(frameCount);
            }

            return _cachedDocument;
        }

        /// <summary>
        /// スナップショットを AI 向けの圧縮テキストへ変換します。
        /// 座標や内部詳細を省いてトークン効率を上げるためです。
        /// </summary>
        public static string ToCompactText(UiSnapshotDocument document, string scope = null)
        {
            return UiSnapshotCompactTextFormatter.ToCompactText(document, scope);
        }

        /// <summary>
        /// 2 つのスナップショット差分を返します。
        /// 操作結果が空振りかどうかを要素単位で即判定できるようにします。
        /// </summary>
        public static UiSnapshotDiff Compare(UiSnapshotDocument before, UiSnapshotDocument after)
        {
            return UiSnapshotComparer.Compare(before, after);
        }

        /// <summary>
        /// スナップショットを JSON へ保存します。
        /// 人が見つけやすい既定出力先へ寄せて、他ツールと成果物の置き場を揃えます。
        /// </summary>
        public static string Save(UiSnapshotDocument document, string outputDirectory = null)
        {
            return UiSnapshotStorage.Save(document, outputDirectory);
        }

        /// <summary>
        /// シナリオの `snapshot` 名と成果物名を一致させ、後続ツールがステップ名で証拠へ到達できるようにします。
        /// </summary>
        public static string Save(UiSnapshotDocument document, string outputDirectory, string fileNameWithoutExtension)
        {
            return UiSnapshotStorage.Save(document, outputDirectory, fileNameWithoutExtension);
        }
    }
}
#endif
