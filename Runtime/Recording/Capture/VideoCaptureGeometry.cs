#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace UniTestify
{
    /// <summary>録画開始時のキャプチャ範囲を固定し、後続の画面変更を検出します。</summary>
    internal sealed class VideoCaptureGeometry
    {
        private int _sourceCaptureWidth;
        private int _sourceCaptureHeight;
        private RectInt _captureCropRect;
        private bool _hasLoggedCaptureRectChange;

        /// <summary>読み戻し元の画面幅です。</summary>
        internal int SourceWidth => _sourceCaptureWidth;

        /// <summary>読み戻し元の画面高さです。</summary>
        internal int SourceHeight => _sourceCaptureHeight;

        /// <summary>開始時に固定した切り抜き矩形です。</summary>
        internal RectInt CropRect => _captureCropRect;

        /// <summary>保存する画像の幅です。</summary>
        internal int Width => _captureCropRect.width;

        /// <summary>保存する画像の高さです。</summary>
        internal int Height => _captureCropRect.height;

        /// <summary>録画開始時の画面寸法とカメラ矩形を固定します。</summary>
        internal void Initialize()
        {
            ResolveCaptureGeometry(out _sourceCaptureWidth, out _sourceCaptureHeight, out _captureCropRect);
        }

        /// <summary>読み戻し元から画像を切り抜く必要があるか返します。</summary>
        internal bool RequiresCropping()
        {
            return _captureCropRect.x != 0 ||
                _captureCropRect.y != 0 ||
                _captureCropRect.width != _sourceCaptureWidth ||
                _captureCropRect.height != _sourceCaptureHeight;
        }

        /// <summary>録画中の寸法変更を一度だけ警告し、開始時の寸法を維持します。</summary>
        internal void WarnIfCaptureGeometryChanged()
        {
            if (_hasLoggedCaptureRectChange)
            {
                return;
            }

            ResolveCaptureGeometry(out var sourceCaptureWidth, out var sourceCaptureHeight, out var captureCropRect);
            if (sourceCaptureWidth == _sourceCaptureWidth &&
                sourceCaptureHeight == _sourceCaptureHeight &&
                captureCropRect == _captureCropRect)
            {
                return;
            }

            _hasLoggedCaptureRectChange = true;
            UnityEngine.Debug.LogWarning(
                $"[VideoRecorder] 録画開始後に描画矩形が変化しました。開始時 raw={_sourceCaptureWidth}x{_sourceCaptureHeight} crop={FormatRect(_captureCropRect)} 現在 raw={sourceCaptureWidth}x{sourceCaptureHeight} crop={FormatRect(captureCropRect)}。録画サイズは開始時のまま維持します。");
        }

        private static void ResolveCaptureGeometry(out int sourceCaptureWidth, out int sourceCaptureHeight, out RectInt captureCropRect)
        {
            sourceCaptureWidth = Mathf.Max(1, Screen.width);
            sourceCaptureHeight = Mathf.Max(1, Screen.height);
            captureCropRect = new RectInt(0, 0, sourceCaptureWidth, sourceCaptureHeight);

            if (!TryGetTargetCameraPixelRect(out var cameraPixelRect))
            {
                return;
            }

            captureCropRect = ClampPixelRect(cameraPixelRect, sourceCaptureWidth, sourceCaptureHeight);
        }

        private static bool TryGetTargetCameraPixelRect(out Rect pixelRect)
        {
            pixelRect = default;

            var mainCamera = Camera.main;
            if (IsCaptureTargetCamera(mainCamera))
            {
                pixelRect = mainCamera.pixelRect;
                return true;
            }

            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (var cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            {
                var camera = cameras[cameraIndex];
                if (!IsCaptureTargetCamera(camera))
                {
                    continue;
                }

                pixelRect = camera.pixelRect;
                return true;
            }

            return false;
        }

        private static bool IsCaptureTargetCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            if (!camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                return false;
            }

            return true;
        }

        /// <summary>カメラ矩形を画面内の少なくとも一画素を含む整数矩形へ制限します。</summary>
        internal static RectInt ClampPixelRect(Rect pixelRect, int sourceCaptureWidth, int sourceCaptureHeight)
        {
            var minX = Mathf.Clamp(Mathf.FloorToInt(pixelRect.xMin), 0, Mathf.Max(0, sourceCaptureWidth - 1));
            var minY = Mathf.Clamp(Mathf.FloorToInt(pixelRect.yMin), 0, Mathf.Max(0, sourceCaptureHeight - 1));
            var maxX = Mathf.Clamp(Mathf.CeilToInt(pixelRect.xMax), minX + 1, sourceCaptureWidth);
            var maxY = Mathf.Clamp(Mathf.CeilToInt(pixelRect.yMax), minY + 1, sourceCaptureHeight);
            return new RectInt(minX, minY, maxX - minX, maxY - minY);
        }

        private static string FormatRect(RectInt rect)
        {
            return $"({rect.x},{rect.y},{rect.width},{rect.height})";
        }
    }
}
#endif
