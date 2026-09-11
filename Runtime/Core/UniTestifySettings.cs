#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniTestify
{
    /// <summary>初回起動前に外部ファイルを配置できない実機向けに、自律実行と HTTP 接続の設定をビルドへ含めます。</summary>
    [CreateAssetMenu(fileName = ResourcePath, menuName = "UniTestify/Settings")]
    public sealed class UniTestifySettings : ScriptableObject
    {
        /// <summary>Resources で型付き設定を読むための共通名です。</summary>
        internal const string ResourcePath = "UniTestifySettings";

        /// <summary>起動シーンの初期化に猶予を与える既定の待機秒数です。</summary>
        public const float DefaultAutorunDelaySeconds = 2f;

        /// <summary>USB 転送と直接接続で共通に使う HTTP ポートの既定値です。</summary>
        public const int DefaultHttpPort = 7910;

        /// <summary>自律実行するシナリオのパスです。空なら自律実行しません。</summary>
        public string autorunScenarioPath = string.Empty;

        /// <summary>起動シーン読み込み後に待つ実時間の秒数です。</summary>
        public float autorunDelaySeconds = DefaultAutorunDelaySeconds;

        /// <summary>起動シーンの読み込み後に HTTP 入口を有効にします。</summary>
        public bool httpEnabled;

        /// <summary>HTTP の待受ポートです。0 は起動時に空きポートを割り当てます。</summary>
        public int httpPort = DefaultHttpPort;

        /// <summary>Bearer 認証のトークンです。空なら起動ごとに生成します。</summary>
        public string httpToken = string.Empty;

        /// <summary>ループバックに加えて同一 LAN からの接続を許可します。既定は無効です。</summary>
        public bool httpAllowLan;
    }
}
#endif
