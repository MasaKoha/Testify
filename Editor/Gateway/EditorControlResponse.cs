#nullable enable
using System;
using UnityEngine;

namespace UniTestify.Editor
{
    /// <summary>Editor 操作の結果を ok と message だけで返します。</summary>
    [Serializable]
    public sealed class EditorControlResponse
    {
        /// <summary>要求を受理または処理できたかを示します。</summary>
        public bool ok;

        /// <summary>結果、拒否理由、または status の状態値です。</summary>
        public string message = string.Empty;

        /// <summary>状態遷移の完了と区別できる受理応答を生成します。</summary>
        public static EditorControlResponse Accepted(string operation)
        {
            return Success($"{operation} の要求を受理しました。状態は status で確認してください。");
        }

        /// <summary>成功内容を簡素な応答契約へ変換します。</summary>
        public static EditorControlResponse Success(string message)
        {
            return new EditorControlResponse { ok = true, message = message };
        }

        /// <summary>失敗理由を簡素な応答契約へ変換します。</summary>
        public static EditorControlResponse Failure(string message)
        {
            return new EditorControlResponse { ok = false, message = message };
        }

        /// <summary>メールボックスへ公開する JSON を生成します。</summary>
        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
