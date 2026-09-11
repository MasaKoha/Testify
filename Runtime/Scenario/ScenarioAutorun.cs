#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UniTestify
{
    /// <summary>起動時の設定だけでシナリオを一度実行し、メールボックスなしで完了をファイルへ残します。</summary>
    internal sealed class ScenarioAutorun : MonoBehaviour
    {
        private const string ConfigurationFileName = "scenario-autorun.json";
        private const string CompletionFileName = "scenario-autorun.done.json";
        private const string CompletionTemporaryFileName = CompletionFileName + ".tmp";
        private const string ResultFileName = "result.json";
        private const string ScenarioArgument = "-unitestify-scenario";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfConfigured()
        {
            try
            {
                var settings = Resources.Load<UniTestifySettings>(UniTestifySettings.ResourcePath);
                var configurationPath = Path.Combine(DebugOutputPath.DirectoryPath, ConfigurationFileName);
                var configurationJson = File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : null;
                var configuration = ResolveConfiguration(configurationJson,
                    settings == null ? string.Empty : settings.autorunScenarioPath,
                    settings == null ? UniTestifySettings.DefaultAutorunDelaySeconds : settings.autorunDelaySeconds,
                    GetCommandLineArguments());
                if (string.IsNullOrWhiteSpace(configuration.Path))
                {
                    return;
                }

                Directory.CreateDirectory(DebugOutputPath.DirectoryPath);
                // 前回の完了を今回の実行結果と誤認させないよう、待機開始前に取り除く。
                File.Delete(Path.Combine(DebugOutputPath.DirectoryPath, CompletionFileName));
                var autorunObject = new GameObject(nameof(ScenarioAutorun));
                DontDestroyOnLoad(autorunObject);
                var autorun = autorunObject.AddComponent<ScenarioAutorun>();
                autorun.StartCoroutine(autorun.RunAsync(configuration.Path, configuration.Name, configuration.DelaySeconds));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[ScenarioAutorun] 起動できませんでした: {exception.Message}");
            }
        }

        /// <summary>ビルド設定へ JSON の指定フィールド、起動引数のパスの順に適用します。JSON がない場合は null を渡します。</summary>
        internal static (string Path, string Name, float DelaySeconds) ResolveConfiguration(string configurationJson,
            string defaultScenarioPath = "", float defaultDelaySeconds = UniTestifySettings.DefaultAutorunDelaySeconds,
            string[] commandLineArguments = null)
        {
            var configuration = new Configuration { path = defaultScenarioPath, delaySeconds = defaultDelaySeconds };
            if (configurationJson != null)
            {
                ApplyConfigurationJson(configurationJson, configuration);
            }

            ApplyCommandLineArguments(commandLineArguments ?? Array.Empty<string>(), configuration);
            AiCommandArguments.ValidateDuration(configuration.delaySeconds, nameof(configuration.delaySeconds), false);
            return (configuration.path ?? string.Empty, configuration.name ?? string.Empty, configuration.delaySeconds);
        }

        private static void ApplyConfigurationJson(string configurationJson, Configuration configuration)
        {
            var members = AiJsonObject.Parse(configurationJson);
            ValidateTextMember(members, nameof(configuration.path));
            ValidateTextMember(members, nameof(configuration.name));
            if (members.TryGetValue(nameof(configuration.delaySeconds), out var delayJson))
            {
                if (!float.TryParse(delayJson, NumberStyles.Float, CultureInfo.InvariantCulture, out var delaySeconds))
                {
                    throw new ArgumentException("delaySeconds は数値で指定してください。");
                }

                AiCommandArguments.ValidateDuration(delaySeconds, nameof(configuration.delaySeconds), false);
            }

            // 未指定フィールドと明示した 0 秒を区別し、ビルド設定の既定値を保つ。
            JsonUtility.FromJsonOverwrite(configurationJson, configuration);
        }

        private static void ValidateTextMember(Dictionary<string, string> members, string memberName)
        {
            if (members.TryGetValue(memberName, out var value) && !value.StartsWith("\"", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{memberName} は文字列で指定してください。");
            }
        }

        private static string[] GetCommandLineArguments()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return Environment.GetCommandLineArgs();
#else
            return Array.Empty<string>();
#endif
        }

        private static void ApplyCommandLineArguments(string[] arguments, Configuration configuration)
        {
            for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            {
                if (arguments[argumentIndex] != ScenarioArgument)
                {
                    continue;
                }

                var pathIndex = argumentIndex + 1;
                if (pathIndex >= arguments.Length || string.IsNullOrWhiteSpace(arguments[pathIndex]) ||
                    arguments[pathIndex].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{ScenarioArgument} の後にシナリオのパスが必要です。");
                }

                configuration.path = arguments[pathIndex];
                argumentIndex = pathIndex;
            }
        }

        private IEnumerator<object> RunAsync(string scenarioPath, string scenarioName, float delaySeconds)
        {
            try
            {
                if (delaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(delaySeconds);
                }

                if (!TryStartScenario(scenarioPath, scenarioName, out var runner, out var resultFilePath))
                {
                    yield break;
                }

                // perf: 実行中は参照の生存確認だけを行い、結果 JSON の読み込み・確保を完了後の一回に限定する。
                // 空シナリオは Run 内で完了し得るため、起動後のイベント購読には依存しない。
                while (runner != null)
                {
                    yield return null;
                }

                SaveCompletion(resultFilePath);
            }
            finally
            {
                Destroy(gameObject);
            }
        }

        private static bool TryStartScenario(string scenarioPath, string scenarioName,
            out UiScenarioRunner runner, out string resultFilePath)
        {
            runner = null;
            resultFilePath = string.Empty;
            try
            {
                var resolvedPath = DebugOutputPath.ResolveRelative(scenarioPath);
                var scenarioJson = File.ReadAllText(resolvedPath);
                AiJsonObject.Parse(scenarioJson);
                var scenario = JsonUtility.FromJson<UiScenario>(scenarioJson);
                if (scenario == null)
                {
                    throw new ArgumentException($"シナリオ JSON の読み込みに失敗しました。 path={resolvedPath}");
                }

                UiScenarioJsonPresence.Apply(scenarioJson, scenario);
                var resolvedName = string.IsNullOrEmpty(scenarioName) ? Path.GetFileNameWithoutExtension(resolvedPath) : scenarioName;
                var plannedPath = Path.GetFullPath(UiScenarioRunner.CreateResultFilePath(resolvedName));
                // 再起動や同名シナリオの連続実行でも過去の結果を上書きしない。
                var resultDirectory = Path.GetDirectoryName(plannedPath) + "-" + Guid.NewGuid().ToString("N");
                resultFilePath = Path.Combine(resultDirectory, ResultFileName);
                runner = UiScenarioRunner.Run(scenario, resolvedName, resultFilePath);
                return true;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[ScenarioAutorun] シナリオを開始できませんでした: {exception.Message}");
                return false;
            }
        }

        private static void SaveCompletion(string resultFilePath)
        {
            try
            {
                var result = JsonUtility.FromJson<ScenarioResult>(File.ReadAllText(resultFilePath));
                if (result == null || string.IsNullOrEmpty(result.verdict))
                {
                    throw new InvalidDataException("シナリオの結果に verdict がありません。");
                }

                var completion = new Completion { path = resultFilePath, verdict = result.verdict };
                WriteCompletion(completion);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[ScenarioAutorun] 完了ファイルを保存できませんでした: {exception.Message}");
            }
        }

        private static void WriteCompletion(Completion completion)
        {
            var completionPath = Path.Combine(DebugOutputPath.DirectoryPath, CompletionFileName);
            var temporaryPath = Path.Combine(DebugOutputPath.DirectoryPath, CompletionTemporaryFileName);
            try
            {
                // 完了ファイルの存在だけで回収する外部プロセスへ、書きかけの JSON を公開しない。
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(completion, true));
                File.Move(temporaryPath, completionPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        /// <summary>自律実行の外部設定だけを保持し、Resources の元アセットへの変更を避けます。</summary>
        [Serializable]
        private sealed class Configuration
        {
            /// <summary>実行するシナリオのパスです。空文字は自律実行を無効にします。</summary>
            public string path = string.Empty;
            /// <summary>結果へ付ける任意の名前です。省略時はシナリオのファイル名を使います。</summary>
            public string name = string.Empty;
            /// <summary>起動シーン読み込み後に待つ実時間の秒数です。</summary>
            public float delaySeconds;
        }

        /// <summary>既存の結果 JSON を変更せず、外部から完了と取得先を判断できるようにします。</summary>
        [Serializable]
        private sealed class Completion
        {
            /// <summary>この起動で保存された result.json の絶対パスです。</summary>
            public string path;
            /// <summary>既存ランナーが保存した pass / fail / error の判定です。</summary>
            public string verdict;
        }
    }
}
#endif
