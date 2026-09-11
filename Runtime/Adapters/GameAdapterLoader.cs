#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace UniTestify
{
    /// <summary>任意の Editor コンパイラを Runtime へ依存させず、アダプタ注入の成否を共通化します。</summary>
    internal static class GameAdapterLoader
    {
        /// <summary>Pipeline 側が Editor の初期化時に登録するコンパイル・登録処理です。未導入時は null です。</summary>
        internal static Func<string, GameAdapterLoadResult> Loader;

        /// <summary>単独 op とセッション開始で同じ注入処理と失敗メッセージを使います。</summary>
        internal static GameAdapterLoadResult Load(string directory)
        {
            var loader = Loader;
            if (loader == null)
            {
                return new GameAdapterLoadResult(false, "TESTIFY_PIPELINE が無効です");
            }

            try
            {
                return loader(directory);
            }
            catch (Exception exception)
            {
                // 反射呼び出しの内部例外も残し、利用側のソースを修正できるようにする。
                return new GameAdapterLoadResult(false, exception.ToString());
            }
        }
    }
}
#endif
