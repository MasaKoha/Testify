#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>カメラや GPU を使わず、切り抜き矩形の丸めと境界処理を固定します。</summary>
    public sealed class VideoCaptureGeometryTest
    {
        private const int SourceWidth = 100;
        private const int SourceHeight = 80;

        /// <summary>画面外・小数座標・ゼロ面積でも、既存と同じ整数矩形を返します。</summary>
        [TestCase(-5f, -2f, 110f, 90f, 0, 0, 100, 80)]
        [TestCase(10.2f, 20.2f, 30.1f, 10.1f, 10, 20, 31, 11)]
        [TestCase(120f, 90f, 0f, 0f, 99, 79, 1, 1)]
        public void ClampPixelRectPreservesRoundingAndBounds(
            float sourceLeft, float sourceBottom, float sourceWidth, float sourceHeight,
            int expectedLeft, int expectedBottom, int expectedWidth, int expectedHeight)
        {
            var rectangle = new Rect(sourceLeft, sourceBottom, sourceWidth, sourceHeight);

            var result = VideoCaptureGeometry.ClampPixelRect(rectangle, SourceWidth, SourceHeight);

            Assert.That(result, Is.EqualTo(new RectInt(expectedLeft, expectedBottom, expectedWidth, expectedHeight)));
        }
    }
}
#endif
