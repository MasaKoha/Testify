#if UNITY_EDITOR
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UniTestify.Tests
{
    /// <summary>legacy Text の収集・ラベル抽出・文字判定を PlayMode なしで検証します。</summary>
    public sealed class UiSnapshotLegacyTextTest
    {
        private const string RootName = "__UniTestifyLegacyTextRoot__";
        private const string TextName = "Message";
        private const string TextContent = "__UniTestifyLegacyText__準備完了";
        private const string PlaceholderContent = "__UniTestifyLegacyPlaceholder__名前を入力";
        private const int MaximumLabelLength = 80;
        private const float TextWidth = 160f;
        private const float TextHeight = 40f;
        private GameObject _root;
        private Canvas _canvas;

        /// <summary>既存シーンに依存しない Canvas を用意します。</summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(RootName, typeof(RectTransform));
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// <summary>生成した UI 階層を残さないよう子も含めて破棄します。</summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        /// <summary>Canvas 配下の文言を既存の Text 種別と圧縮表記で観測できます。</summary>
        [Test]
        public void CollectsLegacyTextUnderCanvas()
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            var elements = UiSnapshotElementCollector.CollectElements(textObject.gameObject);
            var element = elements.Find(candidate => candidate.path == RootName + "/" + TextName);

            Assert.That(element, Is.Not.Null);
            Assert.That(element.kind, Is.EqualTo("Text"));
            Assert.That(element.label, Is.EqualTo(TextContent));
            Assert.That(element.interactable, Is.False);
            Assert.That(element.focused, Is.True);
            Assert.That(element.value, Is.Empty);
            var snapshot = new UiSnapshotDocument { elements = new[] { element } };
            Assert.That(UiSnapshot.ToCompactText(snapshot, "all"), Does.Contain("[Text]"));
            Assert.That(UiSnapshot.ToCompactText(snapshot, "all"), Does.Contain("「" + TextContent + "」"));
        }

        /// <summary>空文字は矩形や観測要素を生成せず、収集結果からも除外します。</summary>
        [TestCase("")]
        [TestCase(null)]
        public void ExcludesEmptyLegacyText(string content)
        {
            var textObject = CreateLegacyText(_root.transform, TextName, content);

            Assert.That(UiSnapshotElementFactory.TryCreateTextElement(textObject, null, out var element), Is.False);
            Assert.That(element, Is.Null);
            Assert.That(UiSnapshotElementCollector.CollectElements(null).Exists(
                candidate => candidate.path == RootName + "/" + TextName), Is.False);
        }

        /// <summary>非表示の Text と観測オーバーレイを独立要素へ混ぜません。</summary>
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ExcludesDisabledOrOverlayLegacyText(bool textEnabled, bool hasOverlayMarker)
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            textObject.enabled = textEnabled;
            if (hasOverlayMarker)
            {
                _root.AddComponent<UiOverlayMarker>();
            }

            Assert.That(UiSnapshotElementCollector.CollectElements(null).Exists(
                element => element.path == RootName + "/" + TextName), Is.False);
        }

        /// <summary>ボタンの文言を親ラベルへまとめ、既存の label 指定でも解決できます。</summary>
        [Test]
        public void CollectsLegacySelectableLabelOnce()
        {
            var button = CreateUiObject(_root.transform, "Button").AddComponent<Button>();
            button.gameObject.AddComponent<Image>();
            var textObject = CreateLegacyText(button.transform, TextName, TextContent);

            var elements = UiSnapshotElementCollector.CollectElements(null);
            var element = elements.Find(candidate => candidate.path == RootName + "/Button");

            Assert.That(element, Is.Not.Null);
            Assert.That(element.kind, Is.EqualTo("Button"));
            Assert.That(element.label, Is.EqualTo(TextContent));
            Assert.That(elements.Exists(candidate => candidate.path == RootName + "/Button/" + TextName), Is.False);
            Assert.That(UiInputLocator.FindByLabel(TextContent), Is.EqualTo(button.gameObject));
            textObject.enabled = false;
            Assert.That(UiVisibilityUtility.FindSelectableLabel(button.gameObject, MaximumLabelLength), Is.Empty);
        }

        /// <summary>入れ子の Selectable が所有するラベルを親へ重複計上しません。</summary>
        [Test]
        public void IgnoresLegacyLabelsOwnedByNestedSelectables()
        {
            var parentButton = CreateUiObject(_root.transform, "Parent").AddComponent<Button>();
            var childButton = CreateUiObject(parentButton.transform, "Child").AddComponent<Button>();
            CreateLegacyText(childButton.transform, TextName, TextContent);

            Assert.That(UiVisibilityUtility.FindSelectableLabel(parentButton.gameObject, MaximumLabelLength), Is.Empty);
            Assert.That(UiVisibilityUtility.FindSelectableLabel(childButton.gameObject, MaximumLabelLength), Is.EqualTo(TextContent));
        }

        /// <summary>両入力欄の legacy placeholder を親ラベルとして観測し、種別・値の契約を維持します。</summary>
        [TestCase(false, "Selectable")]
        [TestCase(true, "Input")]
        public void CollectsLegacyInputPlaceholder(bool useTextMeshProInput, string expectedKind)
        {
            var inputObject = CreateUiObject(_root.transform, "Input");
            inputObject.AddComponent<Image>();
            var placeholder = CreateLegacyText(inputObject.transform, "Placeholder", PlaceholderContent);
            if (useTextMeshProInput)
            {
                inputObject.AddComponent<TMP_InputField>().placeholder = placeholder;
            }
            else
            {
                var inputField = inputObject.AddComponent<InputField>();
                inputField.textComponent = CreateLegacyText(inputObject.transform, TextName, string.Empty);
                inputField.placeholder = placeholder;
            }

            var elements = UiSnapshotElementCollector.CollectElements(null);
            var element = elements.Find(candidate => candidate.path == RootName + "/Input");

            Assert.That(element, Is.Not.Null);
            Assert.That(element.kind, Is.EqualTo(expectedKind));
            Assert.That(element.label, Is.EqualTo(PlaceholderContent));
            Assert.That(element.value, Is.Empty);
            Assert.That(elements.Exists(candidate => candidate.path == RootName + "/Input/Placeholder"), Is.False);
            Assert.That(UiInputLocator.HasVisibleText(PlaceholderContent), Is.True);
        }

        /// <summary>文字色と CanvasGroup の透明度を TMP と同じ閾値で判定します。</summary>
        [TestCase(1f, 1f, true)]
        [TestCase(0f, 1f, false)]
        [TestCase(0.01f, 1f, false)]
        [TestCase(1f, 0.01f, false)]
        public void HasVisibleLegacyTextChecksAlpha(float textAlpha, float groupAlpha, bool expectedVisible)
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            textObject.color = new Color(1f, 1f, 1f, textAlpha);
            _root.AddComponent<CanvasGroup>().alpha = groupAlpha;

            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.EqualTo(expectedVisible));
        }

        /// <summary>コンポーネント・オブジェクト・Canvas の無効化を文字待機へ反映します。</summary>
        [Test]
        public void HasVisibleLegacyTextTracksActivation()
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.True);
            textObject.enabled = false;
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.False);
            textObject.enabled = true;
            textObject.gameObject.SetActive(false);
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.False);
            textObject.gameObject.SetActive(true);
            _canvas.enabled = false;
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.False);
        }

        /// <summary>ignoreParentGroups による祖先透明度の打ち切りを維持します。</summary>
        [Test]
        public void HasVisibleLegacyTextHonorsIgnoreParentGroups()
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            _root.AddComponent<CanvasGroup>().alpha = 0f;
            var textGroup = textObject.gameObject.AddComponent<CanvasGroup>();
            textGroup.ignoreParentGroups = true;
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.True);
            textGroup.ignoreParentGroups = false;
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.False);
        }

        /// <summary>legacy の観測結果だけで対話・シナリオ双方の文字の存在と不在を判定できます。</summary>
        [TestCase("textVisible", true, true)]
        [TestCase("textVisible", false, false)]
        [TestCase("textAbsent", true, false)]
        [TestCase("textAbsent", false, true)]
        public void TextExpectationsFollowLegacyTextPresence(string kind, bool textActive, bool expectedSuccess)
        {
            var textObject = CreateLegacyText(_root.transform, TextName, TextContent);
            textObject.gameObject.SetActive(textActive);
            var snapshot = new UiSnapshotDocument { elements = UiSnapshotElementCollector.CollectElements(null).ToArray() };
            var expectations = new[] { new ScenarioExpectation { kind = kind, value = TextContent, scope = RootName } };

            Assert.That(new AgentExpectationEvaluator().Evaluate(expectations, snapshot, null), Is.EqualTo(expectedSuccess));
            var failures = new ScenarioExpectationEvaluator().EvaluateExpectations(
                new UiScenarioStep { expect = expectations }, snapshot, null, 0, null, null, null);
            Assert.That(failures.Count, Is.EqualTo(expectedSuccess ? 0 : 1));
        }

        private static Text CreateLegacyText(Transform parent, string name, string content)
        {
            var textObject = CreateUiObject(parent, name).AddComponent<Text>();
            textObject.text = content;
            return textObject;
        }

        private static GameObject CreateUiObject(Transform parent, string name)
        {
            var target = new GameObject(name, typeof(RectTransform));
            var rectTransform = (RectTransform)target.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.sizeDelta = new Vector2(TextWidth, TextHeight);
            return target;
        }

        /// <summary>TMP にも同じ空文字除外を適用し、文字種別による差を作りません。</summary>
        [Test]
        public void ExcludesEmptyTextMeshPro()
        {
            var target = CreateUiObject(_root.transform, TextName);
            // 空文字の判定にフォント設定の読み込みや描画初期化を持ち込まないため。
            target.SetActive(false);
            var textObject = target.AddComponent<TextMeshProUGUI>();
            textObject.text = string.Empty;

            Assert.That(UiSnapshotElementFactory.TryCreateTextElement(textObject, null, out var element), Is.False);
            Assert.That(element, Is.Null);
        }
        /// <summary>派生コンポーネントも Text の探索と文字待機から漏れません。</summary>
        [Test]
        public void CollectsDerivedLegacyText()
        {
            var textObject = CreateUiObject(_root.transform, TextName).AddComponent<DerivedLegacyText>();
            textObject.text = TextContent;

            var elements = UiSnapshotElementCollector.CollectElements(null);

            Assert.That(elements.Exists(element => element.path == RootName + "/" + TextName
                && element.kind == "Text" && element.label == TextContent), Is.True);
            Assert.That(UiInputLocator.HasVisibleText(TextContent), Is.True);
        }

        /// <summary>ゲーム固有の派生 Text も観測対象になることを検証するための型です。</summary>
        private sealed class DerivedLegacyText : Text
        {
        }
    }
}
#endif
