#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>UI を持たないシーンオブジェクトの存在を一回の評価で断定できることを検証します。</summary>
    public sealed class ScenarioExpectationObjectTest
    {
        private const string RootName = "__UniTestifyExpectationRoot__";
        private const string ChildName = "__UniTestifyExpectationChild__";
        private const string TargetPath = RootName + "/" + ChildName;
        private GameObject _root;
        private GameObject _child;

        /// <summary>既存シーンの対象に依存しない非 UI 階層を用意します。</summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(RootName);
            _child = new GameObject(ChildName);
            _child.transform.SetParent(_root.transform);
        }

        /// <summary>生成した階層を子も含めて破棄します。</summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
        }

        /// <summary>自身または祖先が非アクティブなら、不在として両方の評価器で断定します。</summary>
        [TestCase("objectExists", true, true, true)]
        [TestCase("objectExists", true, false, false)]
        [TestCase("objectExists", false, true, false)]
        [TestCase("objectExists", false, false, false)]
        [TestCase("objectAbsent", true, true, false)]
        [TestCase("objectAbsent", true, false, true)]
        [TestCase("objectAbsent", false, true, true)]
        [TestCase("objectAbsent", false, false, true)]
        public void ObjectExpectationUsesActiveHierarchy(string kind, bool rootActive, bool childActive, bool expectedSuccess)
        {
            _root.SetActive(rootActive);
            _child.SetActive(childActive);
            AssertExpectation(kind, TargetPath, expectedSuccess);
        }

        /// <summary>名前とパス断片を FindTarget と同じ対象指定として受け付けます。</summary>
        [TestCase(ChildName)]
        [TestCase(TargetPath)]
        public void ObjectExpectationResolvesTargetSpecification(string target)
        {
            AssertExpectation("objectExists", target, true);
            AssertExpectation("objectAbsent", target, false);
        }

        /// <summary>シーンに存在しない対象の断定は即座に真偽を返します。</summary>
        [TestCase("objectExists", false)]
        [TestCase("objectAbsent", true)]
        public void MissingObjectIsEvaluatedOnce(string kind, bool expectedSuccess)
        {
            AssertExpectation(kind, RootName + "/Missing", expectedSuccess);
        }

        /// <summary>遷移前の未達を保持せず、次の一回評価でアクティブ化を検出します。</summary>
        [Test]
        public void ActivationChangesTheNextAssertion()
        {
            _root.SetActive(false);
            AssertExpectation("objectExists", TargetPath, false);
            _root.SetActive(true);
            AssertExpectation("objectExists", TargetPath, true);
        }

        private static void AssertExpectation(string kind, string target, bool expectedSuccess)
        {
            var expectations = new[] { new ScenarioExpectation { kind = kind, target = target } };
            var snapshot = new UiSnapshotDocument();
            var failures = new ScenarioExpectationEvaluator().EvaluateExpectations(
                new UiScenarioStep { expect = expectations }, snapshot, null, 0, null, null, null);
            Assert.That(failures.Count, Is.EqualTo(expectedSuccess ? 0 : 1));
            if (!expectedSuccess)
            {
                Assert.That(failures[0].kind, Is.EqualTo(kind));
                Assert.That(failures[0].target, Is.EqualTo(target));
                Assert.That(failures[0].message, Does.Contain("GameObject"));
            }

            Assert.That(new AgentExpectationEvaluator().Evaluate(expectations, snapshot, null), Is.EqualTo(expectedSuccess));
        }
    }
}
#endif
