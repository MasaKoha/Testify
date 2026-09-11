#if TESTIFY_PIPELINE && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;

namespace UniTestify.Pipeline
{
    /// <summary>Editor で外部ソースをコンパイルして Runtime の登録窓口へ渡します。プレイヤービルドには含めません。</summary>
    internal static class AiAdapterInjector
    {
        // 設計書の基準は 0.4.0-exp.1。現物の 0.6.0-exp.1 では型・メソッドとも internal のため反射で呼ぶ。
        private const string CompilerTypeName = "Unity.Pipeline.Compilation.HotReloadCompiler";
        private const string CompileMethodName = "CompileSourceCodeOnMainThread";
        private const string SuccessPropertyName = "IsSuccess";
        private const string AssemblyNamePropertyName = "AssemblyName";
        private const string ErrorPropertyName = "Error";
        private const string ErrorDetailsPropertyName = "ErrorDetails";
        private const string DiagnosticsPropertyName = "Diagnostics";
        private const string AdapterAssemblyName = "UniTestifyAdapters";
        private const string SourceFilePattern = "*.cs";
        private const int RequiredCompileArgumentCount = 2;

        [InitializeOnLoadMethod]
        private static void RegisterLoader()
        {
            GameAdapterLoader.Loader = Load;
        }

        private static GameAdapterLoadResult Load(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("directory が必要です。", nameof(directory));
            }

            var source = ReadSource(DebugOutputPath.ResolveRelative(directory));
            var compilationResult = Compile(source);
            if (!ReadProperty<bool>(compilationResult, SuccessPropertyName))
            {
                return new GameAdapterLoadResult(false, ReadCompilationFailure(compilationResult));
            }

            var assemblyName = ReadProperty<string>(compilationResult, AssemblyNamePropertyName);
            var assembly = FindCompiledAssembly(assemblyName);
            var registeredCount = GameAdapterTypeBinder.Bind(assembly.GetTypes());
            return new GameAdapterLoadResult(true, $"アダプタを登録しました。登録型数={registeredCount}");
        }

        private static string ReadSource(string directory)
        {
            var sourcePaths = Directory.GetFiles(directory, SourceFilePattern, SearchOption.TopDirectoryOnly);
            Array.Sort(sourcePaths, StringComparer.Ordinal);
            if (sourcePaths.Length == 0)
            {
                throw new InvalidOperationException($"アダプタソース（{SourceFilePattern}）がありません: {directory}");
            }

            var source = new StringBuilder();
            foreach (var sourcePath in sourcePaths)
            {
                // ファイル末尾の行コメントが次のソースを飲み込まないよう、必ず改行を挟む。
                source.AppendLine(File.ReadAllText(sourcePath));
            }

            return source.ToString();
        }

        private static object Compile(string source)
        {
            var compilerType = typeof(CliCommandAttribute).Assembly.GetType(CompilerTypeName, true);
            var compileMethod = compilerType.GetMethod(CompileMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (compileMethod == null)
            {
                throw new MissingMethodException(CompilerTypeName, CompileMethodName);
            }

            var parameters = compileMethod.GetParameters();
            var arguments = new object[parameters.Length];
            arguments[0] = source;
            arguments[1] = AdapterAssemblyName;
            // 現物では末尾に保存先・PDB・インタプリタの省略可能引数がある。通常の既定コンパイルを使う。
            for (var parameterIndex = RequiredCompileArgumentCount; parameterIndex < parameters.Length; parameterIndex++)
            {
                arguments[parameterIndex] = Type.Missing;
            }

            return compileMethod.Invoke(null, arguments);
        }

        private static TValue ReadProperty<TValue>(object instance, string propertyName)
        {
            var resultType = instance.GetType();
            var property = resultType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new MissingMemberException(resultType.FullName, propertyName);
            }

            return (TValue)property.GetValue(instance);
        }

        private static string ReadCompilationFailure(object compilationResult)
        {
            var messages = new List<string>
            {
                ReadProperty<string>(compilationResult, ErrorPropertyName),
                ReadProperty<string>(compilationResult, ErrorDetailsPropertyName),
            };
            messages.AddRange(ReadProperty<List<string>>(compilationResult, DiagnosticsPropertyName));
            return string.Join("\n", messages);
        }

        private static Assembly FindCompiledAssembly(string assemblyName)
        {
            // 戻り値は HotReloadCompileResult。既定の OutputPath は未保存の仮パスなので再ロードしない。
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            throw new InvalidOperationException($"コンパイル済みアセンブリが見つかりません: {assemblyName}");
        }
    }
}
#endif
