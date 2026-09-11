#nullable enable
using System;
using System.IO;
using System.Threading;
using UnityEditor;

namespace UniTestify.Editor
{
    /// <summary>Editor に常駐し、Play 停止中もファイル要求を Editor の更新上で処理します。</summary>
    [InitializeOnLoad]
    internal sealed class EditorControlMailbox
    {
        private const double PollIntervalSeconds = 0.05;
        private const string MailboxDirectoryName = "editor-mailbox";
        private const string RequestPattern = "req-*.json";
        private const int ScanNotRequested = 0;
        private const int ScanRequested = 1;

        private readonly string _directory;
        private readonly FileSystemWatcher _watcher;
        private string[] _requestPaths = Array.Empty<string>();
        private int _requestIndex;
        private int _scanRequested;
        private double _nextPollAt;
        private EditorControlRequest? _pendingRequest;
        private EditorControlResponse? _pendingResponse;
        private string _acceptedOperation = string.Empty;

        static EditorControlMailbox()
        {
            try
            {
                // 更新イベントがインスタンスの寿命を保持し、可変 static 状態を残さない。
                var mailbox = new EditorControlMailbox();
                mailbox.Subscribe();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"[EditorControlMailbox] 起動できませんでした: {exception.Message}");
            }
        }

        private EditorControlMailbox()
        {
            _directory = Path.Combine(DebugOutputPath.DirectoryPath, MailboxDirectoryName);
            Directory.CreateDirectory(_directory);
            _watcher = new FileSystemWatcher(_directory, RequestPattern)
            {
                NotifyFilter = NotifyFilters.FileName,
            };
        }

        private void Subscribe()
        {
            try
            {
                _watcher.Created += OnRequestPublished;
                _watcher.Renamed += OnRequestPublished;
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
                // 監視開始後に初回走査し、ドメインリロード中に公開された要求も拾う。
                _requestPaths = AiMailboxFiles.GetRequests(_directory);
                EditorApplication.update += OnEditorUpdate;
                AssemblyReloadEvents.beforeAssemblyReload += Unsubscribe;
                EditorApplication.quitting += Unsubscribe;
            }
            catch
            {
                Unsubscribe();
                throw;
            }
        }

        private void Unsubscribe()
        {
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Unsubscribe;
            EditorApplication.quitting -= Unsubscribe;
            _watcher.Created -= OnRequestPublished;
            _watcher.Renamed -= OnRequestPublished;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
        }

        private void OnRequestPublished(object sender, FileSystemEventArgs arguments)
        {
            // ファイル通知のスレッドでは Unity API を呼ばず、走査の必要性だけを渡す。
            Interlocked.Exchange(ref _scanRequested, ScanRequested);
        }

        private void OnWatcherError(object sender, ErrorEventArgs arguments)
        {
            // 通知バッファが溢れた場合も、ディスク上の正式要求から回復する。
            Interlocked.Exchange(ref _scanRequested, ScanRequested);
        }

        private void OnEditorUpdate()
        {
            if (_acceptedOperation.Length > 0)
            {
                ApplyAcceptedOperation();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextPollAt)
            {
                return;
            }

            _nextPollAt = now + PollIntervalSeconds;
            try
            {
                PollRequest();
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref _scanRequested, ScanRequested);
                UnityEngine.Debug.LogError($"[EditorControlMailbox] 入出力を再試行します: {exception.Message}");
            }
        }

        private void PollRequest()
        {
            if (_pendingResponse != null)
            {
                CompleteRequest();
                return;
            }

            if (_requestIndex >= _requestPaths.Length)
            {
                // perf: 待機中は通知フラグの確認だけとし、空の GetFiles の確保も避ける。
                if (Interlocked.Exchange(ref _scanRequested, ScanNotRequested) == ScanNotRequested)
                {
                    return;
                }

                // perf: 要求通知がある間隔だけ一度走査し、残りは配列のまま次回へ持ち越す。
                _requestPaths = AiMailboxFiles.GetRequests(_directory);
                _requestIndex = 0;
            }

            if (_requestIndex >= _requestPaths.Length)
            {
                return;
            }

            var requestPath = _requestPaths[_requestIndex];
            if (!File.Exists(requestPath) || File.Exists(AiMailboxFiles.GetResponsePath(requestPath)))
            {
                File.Delete(requestPath);
                _requestIndex++;
                return;
            }

            _pendingResponse = CreateResponse(requestPath);
            CompleteRequest();
        }

        private EditorControlResponse CreateResponse(string requestPath)
        {
            try
            {
                _pendingRequest = EditorControlRequest.FromJson(File.ReadAllText(requestPath));
                var rejection = _pendingRequest.Validate(EditorApplication.isCompiling);
                if (rejection != null)
                {
                    return rejection;
                }

                return _pendingRequest.RequiresAcceptance
                    ? EditorControlResponse.Accepted(_pendingRequest.op)
                    : ExecuteImmediate(_pendingRequest);
            }
            catch (Exception exception)
            {
                return EditorControlResponse.Failure(exception.Message);
            }
        }

        private static EditorControlResponse ExecuteImmediate(EditorControlRequest request)
        {
            switch (request.op)
            {
                case "status":
                    var focusedWindow = EditorWindow.focusedWindow;
                    var focusedWindowName = focusedWindow == null ? string.Empty : focusedWindow.GetType().FullName;
                    return EditorControlResponse.Success(
                        $"isPlaying={EditorApplication.isPlaying} isPaused={EditorApplication.isPaused} " +
                        $"isCompiling={EditorApplication.isCompiling} focusedWindow={focusedWindowName}");
                case "unpause":
                    EditorApplication.isPaused = false;
                    return EditorControlResponse.Success("Pause を解除しました。");
                case "focus_game_view":
                    return FocusView("game");
                case "simulator_view":
                    return FocusView("simulator");
                case "menu":
                    return EditorApplication.ExecuteMenuItem(request.arg)
                        ? EditorControlResponse.Success($"メニューを実行しました: {request.arg}")
                        : EditorControlResponse.Failure($"メニューを実行できませんでした: {request.arg}");
                default:
                    return EditorControlResponse.Failure($"未知の op です: {request.op}");
            }
        }

        private static EditorControlResponse FocusView(string view)
        {
            return PlayModeViewFocus.TryFocus(view)
                ? EditorControlResponse.Success($"{view} をフォーカスしました。")
                : EditorControlResponse.Failure($"{view} をフォーカスできませんでした。");
        }

        private void CompleteRequest()
        {
            var response = _pendingResponse!;
            var requestPath = _requestPaths[_requestIndex];
            var responsePath = AiMailboxFiles.GetResponsePath(requestPath);
            if (!File.Exists(responsePath))
            {
                AiMailboxFiles.WriteAtomic(responsePath, response.ToJson());
            }

            File.Delete(requestPath);
            // 受理応答の公開と要求削除を先に済ませ、Play のリロードで同じ要求を再実行しない。
            if (response.ok && _pendingRequest != null && _pendingRequest.RequiresAcceptance)
            {
                _acceptedOperation = _pendingRequest.op;
            }

            _pendingRequest = null;
            _pendingResponse = null;
            _requestIndex++;
        }

        private void ApplyAcceptedOperation()
        {
            var operation = _acceptedOperation;
            _acceptedOperation = string.Empty;
            // 受理を完了と誤認させないよう、状態変更は応答後の Editor 更新へ送る。
            switch (operation)
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    break;
                case "pause":
                    EditorApplication.isPaused = true;
                    break;
            }
        }
    }
}
