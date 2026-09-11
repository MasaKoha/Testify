#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;

namespace UniTestify
{
    /// <summary>
    /// デバッグ出力先ディレクトリの解決を共通化します。
    /// </summary>
    public static class DebugOutputPath
    {
        private const string OutputDirectoryName = "DebugOutput";

        /// <summary>
        /// Editor はプロジェクト、実機は永続データ領域に成果物を残します。
        /// </summary>
        public static string DirectoryPath
        {
            get
            {
                var assetsDirectoryPath = Application.dataPath;
                var projectRootDirectoryPath = Path.GetDirectoryName(assetsDirectoryPath) ?? string.Empty;
                return Resolve(Application.isEditor, projectRootDirectoryPath, Application.persistentDataPath);
            }
        }

        /// <summary>Unity やファイルシステムへアクセスせず、環境別の出力先を解決します。</summary>
        public static string Resolve(bool isEditor, string projectRoot, string persistentDataPath)
        {
            return Path.Combine(isEditor ? projectRoot : persistentDataPath, OutputDirectoryName);
        }

        /// <summary>相対指定を Editor ではプロジェクト、実機では永続データ領域を基準に絶対パスへ解決します。</summary>
        public static string ResolveRelative(string relativeOrAbsolute)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return ResolveRelative(relativeOrAbsolute, Application.isEditor, projectRoot, Application.persistentDataPath);
        }

        /// <summary>環境を引数で与え、相対・絶対指定の解決を Unity なしで確認できるようにします。</summary>
        internal static string ResolveRelative(string relativeOrAbsolute, bool isEditor, string projectRoot, string persistentDataPath)
        {
            var rootDirectory = isEditor ? projectRoot : persistentDataPath;
            return Path.GetFullPath(Path.Combine(rootDirectory, relativeOrAbsolute));
        }
    }
}
#endif
