#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>公開入口を通じ、分割前のテキスト・差分順序・保存名の契約を固定します。</summary>
    public sealed class UiSnapshotCompatibilityTest
    {
        private const string FocusedPath = "Canvas/Panel/Accept";
        private const string SnapshotName = "step-evidence";

        /// <summary>フォーカス・状態・ゲーム値の表記と改行を一文字単位で維持します。</summary>
        [Test]
        public void CompactTextPreservesExactStatusAndGameText()
        {
            var document = new UiSnapshotDocument
            {
                activeScene = "Menu",
                focusedPath = FocusedPath,
                elements = new[]
                {
                    new UiSnapshotElement
                    {
                        path = FocusedPath, kind = "Button", label = "決定",
                        blockedBy = "Modal", focused = true, value = "on", clipped = true,
                    },
                    new UiSnapshotElement { path = "Canvas/Panel/Empty", kind = "Text", label = " " },
                },
                game = new[] { new UiSnapshotGameEntry { key = "score", value = "1.5" }, null },
            };

            var expected = string.Join(Environment.NewLine,
                "scene=Menu focus=Panel/Accept(決定)",
                "[Button] Panel/Accept 「決定」 !disabled blocked:Modal *focused value:on [clipped]",
                "game: score=1.5");

            Assert.That(UiSnapshot.ToCompactText(document), Is.EqualTo(expected));
        }

        /// <summary>同種の兄弟要素が五件に達したとき、先頭三件と省略件数を残します。</summary>
        [Test]
        public void CompactTextPreservesExactCollapsedSequence()
        {
            var document = new UiSnapshotDocument
            {
                elements = new[]
                {
                    CreateButton("One"), CreateButton("Two"), CreateButton("Three"),
                    CreateButton("Four"), CreateButton("Five"),
                },
            };
            var expected = string.Join(Environment.NewLine,
                "scene=- focus=-",
                "[Button] List/One", "[Button] List/Two", "[Button] List/Three",
                "[Button] Canvas/List …他 2 件");

            Assert.That(UiSnapshot.ToCompactText(document), Is.EqualTo(expected));
        }

        /// <summary>重複パスの後勝ちと差分のパス・フィールド順序を維持します。</summary>
        [Test]
        public void ComparePreservesDuplicatePathAndFieldOrdering()
        {
            var before = new UiSnapshotDocument
            {
                activeScene = "Before", focusedPath = "Shared",
                elements = new[]
                {
                    new UiSnapshotElement { path = "Removed" },
                    new UiSnapshotElement { path = "Shared", label = "first" },
                    new UiSnapshotElement { path = "Shared", label = "last", value = "off" },
                    null, new UiSnapshotElement { path = "" },
                },
            };
            var after = new UiSnapshotDocument
            {
                activeScene = "After", focusedPath = "AddedA",
                elements = new[]
                {
                    new UiSnapshotElement { path = "AddedZ" },
                    new UiSnapshotElement { path = "Shared", label = "new", value = "on" },
                    new UiSnapshotElement { path = "AddedA" },
                },
            };

            var difference = UiSnapshot.Compare(before, after);

            Assert.That(difference.addedPaths, Is.EqualTo(new[] { "AddedA", "AddedZ" }));
            Assert.That(difference.removedPaths, Is.EqualTo(new[] { "Removed" }));
            Assert.That(JsonUtility.ToJson(difference.changed[0]), Is.EqualTo(
                "{\"path\":\"Shared\",\"field\":\"label\",\"before\":\"last\",\"after\":\"new\"}"));
            Assert.That(JsonUtility.ToJson(difference.changed[1]), Is.EqualTo(
                "{\"path\":\"Shared\",\"field\":\"value\",\"before\":\"off\",\"after\":\"on\"}"));
            Assert.That(difference.changed.Length, Is.EqualTo(2));
            Assert.That(difference.focusedBefore, Is.EqualTo("Shared"));
            Assert.That(difference.focusedAfter, Is.EqualTo("AddedA"));
            Assert.That(difference.sceneBefore, Is.EqualTo("Before"));
            Assert.That(difference.sceneAfter, Is.EqualTo("After"));
            Assert.That(difference.isEmpty, Is.False);
        }

        /// <summary>証拠名を変えず、エスケープを含む観測 JSON を整形付きで保存します。</summary>
        [Test]
        public void SavePreservesNamedJsonAndEscapedLabels()
        {
            var outputDirectory = Path.Combine(Path.GetTempPath(), "unitestify-snapshot-" + Guid.NewGuid().ToString("N"));
            var document = new UiSnapshotDocument
            {
                activeScene = "Menu",
                elements = new[] { new UiSnapshotElement { path = FocusedPath, label = "引用\"と改行\nと\\" } },
            };
            try
            {
                var filePath = UiSnapshot.Save(document, outputDirectory, SnapshotName);

                Assert.That(filePath, Is.EqualTo(Path.Combine(outputDirectory, SnapshotName + ".json")));
                Assert.That(File.ReadAllText(filePath), Is.EqualTo(JsonUtility.ToJson(document, true)));
                var restored = JsonUtility.FromJson<UiSnapshotDocument>(File.ReadAllText(filePath));
                Assert.That(restored.elements[0].label, Is.EqualTo(document.elements[0].label));
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        private static UiSnapshotElement CreateButton(string name)
        {
            return new UiSnapshotElement { path = "Canvas/List/" + name, kind = "Button", interactable = true };
        }
    }
}
#endif
