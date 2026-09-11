#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Text;

namespace UniTestify
{
    /// <summary>既存の階層ダンプから、応答量を制限したシーン観測テキストを作ります。</summary>
    public static class SceneHierarchyDumpText
    {
        /// <summary>ルートを深さ 0 とした表示深度の既定上限です。</summary>
        public const int DefaultDepth = 3;

        /// <summary>全シーンを通算した表示ノード数の既定上限です。</summary>
        public const int DefaultMaxNodes = 200;

        private const int IndentWidth = 2;

        /// <summary>名前の部分一致と表示上限を適用し、各ノードに activeInHierarchy を付記します。</summary>
        public static string Format(SceneHierarchyDump dump, int depth = DefaultDepth, int maxNodes = DefaultMaxNodes, string filter = null)
        {
            ValidateLimits(depth, maxNodes);
            var builder = new StringBuilder();
            var writtenNodeCount = 0;
            foreach (var scene in dump.scenes)
            {
                if (!AppendScene(builder, scene, depth, maxNodes, filter, ref writtenNodeCount))
                {
                    builder.Append("... maxNodes=").Append(maxNodes).AppendLine();
                    break;
                }
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>不正な制限値をシーン収集前に拒否できるようにします。</summary>
        internal static void ValidateLimits(int depth, int maxNodes)
        {
            if (depth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), "depth は 0 以上を指定してください。");
            }

            if (maxNodes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNodes), "maxNodes は 1 以上を指定してください。");
            }
        }

        private static bool AppendScene(StringBuilder builder, SceneHierarchyScene scene, int depth, int maxNodes, string filter, ref int writtenNodeCount)
        {
            var nodeDepths = new int[scene.nodes.Length];
            var activeInHierarchy = new bool[scene.nodes.Length];
            var hasWrittenHeader = false;
            // 親が非アクティブな子も正しく表示するため、絞り込みで省く親の状態を先に伝播する。
            foreach (var node in scene.nodes)
            {
                var isRoot = node.parentIndex < 0;
                var nodeDepth = isRoot ? 0 : nodeDepths[node.parentIndex] + 1;
                nodeDepths[node.index] = nodeDepth;
                activeInHierarchy[node.index] = node.activeSelf && (isRoot || activeInHierarchy[node.parentIndex]);
                if (nodeDepth > depth || (!string.IsNullOrEmpty(filter) && node.name.IndexOf(filter, StringComparison.Ordinal) < 0))
                {
                    continue;
                }

                if (writtenNodeCount >= maxNodes)
                {
                    return false;
                }

                if (!hasWrittenHeader)
                {
                    builder.Append("scene=").Append(EscapeName(scene.name)).AppendLine();
                    hasWrittenHeader = true;
                }

                builder.Append(' ', nodeDepth * IndentWidth).Append(EscapeName(node.name))
                    .Append(" activeInHierarchy=").Append(activeInHierarchy[node.index] ? "true" : "false").AppendLine();
                writtenNodeCount++;
            }

            return true;
        }

        private static string EscapeName(string name)
        {
            return name.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
#endif
