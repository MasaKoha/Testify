#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniTestify
{
    /// <summary>省略値を経路間で統一するゲートウェイ引数です。</summary>
    [Serializable]
    internal sealed class AiCommandArguments
    {
        /// <summary>シナリオ全体の既定待機上限です。</summary>
        internal const float DefaultScenarioTimeoutSeconds = 900f;

        /// <summary>対象の準備を待つ既定上限です。</summary>
        internal const float DefaultReadyTimeoutSeconds = 5f;
        /// <summary>入力後の静止確認に使う既定時間です。</summary>
        internal const float DefaultSettleSeconds = 0.35f;
        /// <summary>入力後の待機に使う既定上限です。</summary>
        internal const float DefaultSettleTimeoutSeconds = 10f;
        /// <summary>コンソール応答の既定行数です。</summary>
        internal const int DefaultConsoleCount = 40;

        /// <summary>観測範囲です。visible は可視要素、all は全要素を返します。</summary>
        public string scope = "visible";
        /// <summary>タグ除去後のラベルに部分一致させる検索文字列です。</summary>
        public string label;
        /// <summary>検索対象の要素種別です。省略すると全種別を検索します。</summary>
        public string kind;
        /// <summary>単一 action に指定する事後条件です。</summary>
        public ScenarioExpectation[] expect;
        /// <summary>観測と同じフレームで撮影する任意の成果物名です。</summary>
        public string capture;
        /// <summary>撮影・観測対象の Editor ウィンドウです。空、game、simulator を指定します。</summary>
        public string view = string.Empty;
        /// <summary>ログの対象です。all または error を指定します。</summary>
        public string level = "all";
        /// <summary>対象が操作可能になるまでの実時間の上限です。</summary>
        public float readyTimeoutSeconds = DefaultReadyTimeoutSeconds;

        /// <summary>差分観測を指定します。</summary>
        public bool diffOnly;
        /// <summary>圧縮スナップショットを指定します。</summary>
        public bool compact = true;
        /// <summary>スナップショット保存を指定します。</summary>
        public bool save;
        /// <summary>階層テキストの表示深度です。ルートは 0 です。</summary>
        public int depth = SceneHierarchyDumpText.DefaultDepth;
        /// <summary>階層テキストの全シーン合計の最大ノード数です。</summary>
        public int maxNodes = SceneHierarchyDumpText.DefaultMaxNodes;
        /// <summary>階層テキストに残す GameObject 名の部分一致条件です。</summary>
        public string filter;
        /// <summary>成果物名です。</summary>
        public string name;
        /// <summary>シナリオの絶対パス、または Editor ではプロジェクト・実機では永続データ領域からの相対パスです。</summary>
        public string path;
        /// <summary>シナリオ完了を待つ実時間の上限です。</summary>
        public float scenarioTimeoutSeconds = DefaultScenarioTimeoutSeconds;
        /// <summary>成果物の保存先です。</summary>
        public string directory;
        /// <summary>返すログの末尾行数です。</summary>
        public int count = DefaultConsoleCount;
        /// <summary>入力とシーンの完了後に待つ実時間です。</summary>
        public float settleSeconds = DefaultSettleSeconds;
        /// <summary>各行動の待機上限です。</summary>
        public float settleTimeoutSeconds = DefaultSettleTimeoutSeconds;

        /// <summary>無限待機や負の待機時間を入口で拒否します。</summary>
        internal static void ValidateDuration(float seconds, string name, bool requirePositive)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f || (requirePositive && seconds == 0f))
            {
                throw new ArgumentOutOfRangeException(name, "待機秒数が不正です。");
            }
        }
    }
}
#endif
