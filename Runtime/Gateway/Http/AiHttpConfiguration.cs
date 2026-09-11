#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.Net;
using UnityEngine;

namespace UniTestify
{
    /// <summary>設定アセットを変更せず、HTTP 起動設定へ外部 JSON の指定項目だけを上書きします。</summary>
    [Serializable]
    internal sealed class AiHttpConfiguration
    {
        /// <summary>HTTP 入口を起動するかを示します。</summary>
        public bool httpEnabled;
        /// <summary>待受ポートです。0 は自動割り当てです。</summary>
        public int httpPort = UniTestifySettings.DefaultHttpPort;
        /// <summary>Bearer 認証で一致を要求するトークンです。</summary>
        public string httpToken = string.Empty;
        /// <summary>同一 LAN をループバックに加えて許可するかを示します。</summary>
        public bool httpAllowLan;

        /// <summary>ビルド設定を複製し、JSON の型とポート範囲を検証して実効設定を返します。</summary>
        internal static AiHttpConfiguration Resolve(UniTestifySettings settings, string overrideJson)
        {
            var configuration = new AiHttpConfiguration();
            if (settings != null)
            {
                configuration.httpEnabled = settings.httpEnabled;
                configuration.httpPort = settings.httpPort;
                configuration.httpToken = settings.httpToken;
                configuration.httpAllowLan = settings.httpAllowLan;
            }

            if (overrideJson != null)
            {
                ValidateOverride(overrideJson);
                JsonUtility.FromJsonOverwrite(overrideJson, configuration);
            }

            if (configuration.httpPort < IPEndPoint.MinPort || configuration.httpPort > IPEndPoint.MaxPort)
            {
                throw new ArgumentException("httpPort は 0～65535 の整数で指定してください。");
            }

            if (string.IsNullOrEmpty(configuration.httpToken))
            {
                configuration.httpToken = Guid.NewGuid().ToString("N");
            }

            ValidateToken(configuration.httpToken);
            return configuration;
        }

        private static void ValidateOverride(string json)
        {
            foreach (var member in AiJsonObject.Parse(json))
            {
                switch (member.Key)
                {
                    case nameof(httpEnabled):
                    case nameof(httpAllowLan):
                        if (member.Value != "true" && member.Value != "false")
                        {
                            throw new ArgumentException($"{member.Key} は bool で指定してください。");
                        }
                        break;
                    case nameof(httpPort):
                        if (!int.TryParse(member.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                        {
                            throw new ArgumentException("httpPort は整数で指定してください。");
                        }
                        break;
                    case nameof(httpToken):
                        if (!member.Value.StartsWith("\"", StringComparison.Ordinal))
                        {
                            throw new ArgumentException("httpToken は文字列で指定してください。");
                        }
                        break;
                }
            }
        }

        private static void ValidateToken(string token)
        {
            // ヘッダーに載せられない設定は起動時に拒否し、認証不能な待受を公開しない。
            const char FirstVisibleAscii = '!';
            const char LastVisibleAscii = '~';
            foreach (var character in token)
            {
                if (character < FirstVisibleAscii || character > LastVisibleAscii)
                {
                    throw new ArgumentException("httpToken は空白を含まない ASCII 可視文字で指定してください。");
                }
            }
        }
    }
}
#endif
