#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace UniTestify
{
    /// <summary>コンパイラの型を境界外へ出さず、注入の結果と失敗原文を渡します。</summary>
    internal readonly struct GameAdapterLoadResult
    {
        /// <summary>コンパイルと型登録が完了したかを示します。</summary>
        internal bool Ok { get; }

        /// <summary>登録結果、またはコンパイル診断・例外の原文です。</summary>
        internal string Message { get; }

        /// <summary>共通のコマンド応答へ渡す成否と説明を確定します。</summary>
        internal GameAdapterLoadResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }
    }
}
#endif
