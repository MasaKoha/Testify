#if TESTIFY_PIPELINE
using System;

namespace UniTestify.Pipeline
{
    /// <summary>CLI の型付き引数を JSON にエスケープするための転送形式です。</summary>
    [Serializable]
    internal sealed class AiCliArguments
    {
        /// <summary>成果物名です。</summary>
        public string name;
        /// <summary>保存先です。</summary>
        public string directory;
        /// <summary>フォーカスを適用する撮影・観測対象です。</summary>
        public string view;
        /// <summary>差分観測を指定します。</summary>
        public bool diffOnly;
        /// <summary>圧縮形式を指定します。</summary>
        public bool compact;
        /// <summary>スナップショットの保存を指定します。</summary>
        public bool save;
        /// <summary>シーン階層テキストの表示深度です。</summary>
        public int depth = SceneHierarchyDumpText.DefaultDepth;
        /// <summary>シーン階層テキストに含める最大ノード数です。</summary>
        public int maxNodes = SceneHierarchyDumpText.DefaultMaxNodes;
        /// <summary>シーン階層に適用する GameObject 名の部分一致条件です。</summary>
        public string filter;
    }
}
#endif
