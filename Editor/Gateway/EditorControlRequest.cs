#nullable enable
using System;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>Editor 操作の簡素な JSON 契約を解釈し、副作用なしで要求を検証します。</summary>
    [Serializable]
    public sealed class EditorControlRequest
    {
        /// <summary>Editor に要求する操作名です。</summary>
        public string op = string.Empty;

        /// <summary>menu に渡すメニューパスです。それ以外では空文字列です。</summary>
        public string arg = string.Empty;

        /// <summary>状態の反映を待たず、受理を応答する操作かを示します。</summary>
        public bool RequiresAcceptance => op == "play" || op == "stop" || op == "pause";

        /// <summary>JsonUtility の補正に任せず、壊れた JSON と文字列以外のフィールドを拒否します。</summary>
        public static EditorControlRequest FromJson(string json)
        {
            var members = AiJsonObject.Parse(json);
            foreach (var member in members)
            {
                if ((member.Key == "op" || member.Key == "arg") &&
                    !member.Value.StartsWith("\"", StringComparison.Ordinal))
                {
                    throw new FormatException($"{member.Key} は文字列で指定してください。");
                }
            }

            var request = JsonUtility.FromJson<EditorControlRequest>(json);
            // JsonUtility はフィールド省略時に初期化子を適用しない場合がある。
            request.op ??= string.Empty;
            request.arg ??= string.Empty;
            return request;
        }

        /// <summary>Editor API を呼ばずに拒否応答を生成します。有効な要求なら null を返します。</summary>
        public EditorControlResponse? Validate(bool isCompiling)
        {
            if (isCompiling && op != "status")
            {
                return EditorControlResponse.Failure("コンパイル中は status 以外の操作を受け付けません。");
            }

            switch (op)
            {
                case "status":
                case "play":
                case "stop":
                case "pause":
                case "unpause":
                case "focus_game_view":
                case "simulator_view":
                    return null;
                case "menu":
                    return string.IsNullOrWhiteSpace(arg)
                        ? EditorControlResponse.Failure("menu の arg にメニューパスを指定してください。")
                        : null;
                default:
                    return EditorControlResponse.Failure($"未知の op です: {op}");
            }
        }
    }
}
