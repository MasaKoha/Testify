#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace UniTestify
{
    /// <summary>録画に伴う音声・描画レート・入力可視化の変更と復元を担います。</summary>
    internal sealed class VideoRecordingEnvironment
    {
        private const string AudioFileName = "audio.wav";
        private AudioRecorder _audioRecorder;
        private bool _hasAudio;
        private bool _inputOverlayEnabled = true;
        private bool _shouldHideInputOverlayOnStop;
        private bool _hasOverriddenFrameRate;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;

        /// <summary>音声録音が開始できたかを返します。</summary>
        internal bool HasAudio => _hasAudio;

        /// <summary>音声情報を manifest へ反映するため、開始時の録音器を返します。</summary>
        internal AudioRecorder AudioRecorder => _audioRecorder;

        /// <summary>録画中の入力可視化方針を保持します。</summary>
        internal void Initialize(bool inputOverlayEnabled)
        {
            _inputOverlayEnabled = inputOverlayEnabled;
        }

        /// <summary>
        /// 録画中だけ入力可視化を既定で有効にします。
        /// 非録画時に常時出すと静止画系の観測結果を汚すためです。
        /// </summary>
        internal void ShowInputOverlayIfNeeded()
        {
            if (!_inputOverlayEnabled)
            {
                _shouldHideInputOverlayOnStop = false;
                return;
            }

            if (InputOverlay.IsVisible)
            {
                _shouldHideInputOverlayOnStop = false;
                return;
            }

            InputOverlay.Show();
            _shouldHideInputOverlayOnStop = true;
        }

        /// <summary>
        /// 録画開始時に自動表示した分だけ停止時に戻します。
        /// 手動表示まで巻き込んで消すと既存利用者の意図を壊すためです。
        /// </summary>
        internal void HideInputOverlayIfNeeded()
        {
            if (!_shouldHideInputOverlayOnStop)
            {
                return;
            }

            _shouldHideInputOverlayOnStop = false;
            InputOverlay.Hide();
        }

        /// <summary>音声が要求された場合だけ開始し、開始失敗時は録音器を破棄します。</summary>
        internal void StartAudioRecordingIfNeeded(bool recordAudio, string outputDirectory)
        {
            if (!recordAudio)
            {
                return;
            }

            _audioRecorder = new AudioRecorder();
            var audioFilePath = Path.Combine(outputDirectory, AudioFileName);
            _hasAudio = _audioRecorder.StartRecording(audioFilePath);
            if (_hasAudio)
            {
                return;
            }

            _audioRecorder.Dispose();
            _audioRecorder = null;
            UnityEngine.Debug.LogWarning($"[VideoRecorder] 音声録音を開始できませんでした。 path={audioFilePath}");
        }

        /// <summary>録画中の描画負荷を制限し、復元用に元の設定を保存します。</summary>
        internal void OverrideFrameRateSettings(int framesPerSecond)
        {
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = framesPerSecond;
            _hasOverriddenFrameRate = true;
        }

        /// <summary>録画のために絞った描画レート設定を元へ戻す。二重呼び出しに耐える。</summary>
        internal void RestoreFrameRateSettings()
        {
            if (!_hasOverriddenFrameRate)
            {
                return;
            }

            _hasOverriddenFrameRate = false;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        /// <summary>フレーム末尾で音声サンプルを保存します。</summary>
        internal void PumpAudioFrame()
        {
            if (_audioRecorder == null || !_audioRecorder.IsRecording)
            {
                return;
            }

            _audioRecorder.PumpFrame();
        }

        /// <summary>音声の停止とその内部リソースの解放を録音器へ委譲します。</summary>
        internal void StopAudioRecording()
        {
            if (_audioRecorder == null)
            {
                return;
            }

            try
            {
                _audioRecorder.StopRecording();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[VideoRecorder] 音声録音の停止に失敗しました。 {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
#endif
