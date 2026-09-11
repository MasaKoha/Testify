#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UniTestify
{
    /// <summary>HTTP 要求を Unity のメインスレッドで一件ずつ非同期実行します。</summary>
    public sealed class AiHttpServer : MonoBehaviour
    {
        private const string EnabledFileName = "http.enabled";
        private const string PortFileName = "http.port.json";
        private AiHttpListener _listener;
        private AiHttpExchange _exchange;
        private IEnumerator _execution;
        private string _portFilePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfEnabled()
        {
            AiHttpServer server = null;
            try
            {
                var directory = DebugOutputPath.DirectoryPath;
                var portFilePath = Path.Combine(directory, PortFileName);
                if (File.Exists(portFilePath))
                {
                    File.Delete(portFilePath);
                }

                var overridePath = Path.Combine(directory, EnabledFileName);
                var overrideJson = File.Exists(overridePath) ? File.ReadAllText(overridePath) : null;
                var settings = Resources.Load<UniTestifySettings>(UniTestifySettings.ResourcePath);
                var configuration = AiHttpConfiguration.Resolve(settings, overrideJson);
                if (!configuration.httpEnabled)
                {
                    return;
                }

                Directory.CreateDirectory(directory);
                var serverObject = new GameObject(nameof(AiHttpServer));
                server = serverObject.AddComponent<AiHttpServer>();
                DontDestroyOnLoad(serverObject);
                server._portFilePath = portFilePath;
                server._listener = new AiHttpListener(configuration);
                server.PublishConnection(configuration.httpToken);
                server._listener.Start();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[AiHttpServer] 起動できませんでした: {exception.Message}");
                if (server != null)
                {
                    server.StopServer();
                    Destroy(server.gameObject);
                }
            }
        }

        private void PublishConnection(string token)
        {
            var information = new PortInformation { port = _listener.Port, token = token };
            AiMailboxFiles.WriteAtomic(_portFilePath, JsonUtility.ToJson(information, true));
        }

        private void Update()
        {
            // perf: 待機フレームは状態と既存キューの確認だけにし、要求を受けたときだけ実行用に確保する。
            if (_listener == null)
            {
                return;
            }

            if (_listener.Failure != null)
            {
                UnityEngine.Debug.LogError($"[AiHttpServer] 受付を停止します: {_listener.Failure.Message}");
                StopServer();
                return;
            }

            if (_exchange != null || !_listener.TryDequeue(out _exchange))
            {
                return;
            }

            _execution = AiCommandDispatcher.ExecuteAsync(_exchange.Request, OnCompleted);
            StartCoroutine(_execution);
        }

        private void OnCompleted(AiCommandResponse response)
        {
            _exchange.Complete(JsonUtility.ToJson(response));
            _exchange = null;
            _execution = null;
        }

        private void StopServer()
        {
            try
            {
                StopAllCoroutines();
                // コルーチン内の finally でシーン監視を解除するため、明示的にも破棄する。
                (_execution as IDisposable)?.Dispose();
            }
            finally
            {
                _listener?.Dispose();
                _listener = null;
                _execution = null;
                _exchange = null;
                RemoveConnectionFile();
            }
        }

        private void RemoveConnectionFile()
        {
            if (_portFilePath == null)
            {
                return;
            }

            try
            {
                File.Delete(_portFilePath);
            }
            catch (IOException exception)
            {
                UnityEngine.Debug.LogError($"[AiHttpServer] 接続情報を削除できませんでした: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                UnityEngine.Debug.LogError($"[AiHttpServer] 接続情報を削除できませんでした: {exception.Message}");
            }
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        /// <summary>起動に成功した待受先だけをホストへ知らせる保存形式です。</summary>
        [Serializable]
        private sealed class PortInformation
        {
            /// <summary>自動割り当てを解決した実際のポートです。</summary>
            public int port;
            /// <summary>この起動で有効な Bearer トークンです。</summary>
            public string token;
        }
    }
}
#endif
