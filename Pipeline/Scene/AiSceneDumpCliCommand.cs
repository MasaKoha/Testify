#if TESTIFY_PIPELINE
using Unity.Pipeline.Commands;
using UnityEngine;

namespace UniTestify.Pipeline
{
    /// <summary>共通ディスパッチャのシーン階層観測を Unity 公式 CLI へ公開します。</summary>
    public static class AiSceneDumpCliCommand
    {
        /// <summary>階層をコンパクトテキストで返し、任意で JSON の証拠を保存します。</summary>
        [CliCommand("ai_scene_dump", "シーン階層を取得します。", Tags = new[] { "scene" })]
        public static string Dump(
            [CliArg("depth", "表示する深度の上限。ルートは 0。")] int depth = SceneHierarchyDumpText.DefaultDepth,
            [CliArg("maxNodes", "全シーンを通算した表示ノード数の上限。")] int maxNodes = SceneHierarchyDumpText.DefaultMaxNodes,
            [CliArg("filter", "GameObject 名の部分一致条件。")] string filter = "",
            [CliArg("save", "DebugOutput/scene へ JSON を保存するか。")] bool save = false)
        {
            var response = AiCommandDispatcher.Execute(new AiCommandRequest
            {
                op = "scene.dump",
                args = JsonUtility.ToJson(new AiCliArguments { depth = depth, maxNodes = maxNodes, filter = filter, save = save }),
            });
            return JsonUtility.ToJson(response, true);
        }
    }
}
#endif
