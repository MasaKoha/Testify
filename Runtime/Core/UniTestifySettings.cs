#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniTestify
{
    /// <summary>初回起動前に外部ファイルを配置できない実機向けに、自律実行の設定をビルドへ含めます。</summary>
    [CreateAssetMenu(fileName = ResourcePath, menuName = "UniTestify/Settings")]
    public sealed class UniTestifySettings : ScriptableObject
    {
        /// <summary>Resources で型付き設定を読むための共通名です。</summary>
        internal const string ResourcePath = "UniTestifySettings";

        /// <summary>起動シーンの初期化に猶予を与える既定の待機秒数です。</summary>
        public const float DefaultAutorunDelaySeconds = 2f;

        /// <summary>自律実行するシナリオのパスです。空なら自律実行しません。</summary>
        public string autorunScenarioPath = string.Empty;

        /// <summary>起動シーン読み込み後に待つ実時間の秒数です。</summary>
        public float autorunDelaySeconds = DefaultAutorunDelaySeconds;
    }
}
#endif
