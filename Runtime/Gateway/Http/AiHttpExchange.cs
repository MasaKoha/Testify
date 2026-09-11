#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading.Tasks;

namespace UniTestify
{
    /// <summary>ワーカーで受けた要求と、メインスレッドから返す応答を一対一で結びます。</summary>
    internal sealed class AiHttpExchange
    {
        private readonly TaskCompletionSource<string> _completion =
            new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>メインスレッドへ渡すメールボックス互換の要求です。</summary>
        internal AiCommandRequest Request { get; }

        /// <summary>ワーカーが HTTP コンテキストを保持したまま待つ応答です。</summary>
        internal Task<string> Response => _completion.Task;

        /// <summary>受付順に対応を固定し、別要求の応答と混ざらないようにします。</summary>
        internal AiHttpExchange(AiCommandRequest request)
        {
            Request = request;
        }

        /// <summary>通常完了と停止が競合しても、最初の完了だけを通知します。</summary>
        internal void Complete(string responseJson)
        {
            _completion.TrySetResult(responseJson);
        }
    }
}
#endif
