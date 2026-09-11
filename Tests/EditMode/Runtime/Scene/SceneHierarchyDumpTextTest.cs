#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace UniTestify.Tests
{
    /// <summary>合成したダンプで表示深度・件数制限と非アクティブ状態の伝播を検証します。</summary>
    public sealed class SceneHierarchyDumpTextTest
    {
        private const int RootParentIndex = -1;

        /// <summary>深い子を打ち切っても、後続のルートは表示対象に残します。</summary>
        [Test]
        public void DepthLimitKeepsLaterRoots()
        {
            const int MaximumDepth = 1;
            var text = SceneHierarchyDumpText.Format(CreateHierarchy(), depth: MaximumDepth);
            Assert.That(text, Does.Contain("Root activeInHierarchy=true"));
            Assert.That(text, Does.Contain("\n  Child activeInHierarchy=true"));
            Assert.That(text, Does.Not.Contain("Grandchild"));
            Assert.That(text, Does.Contain("LaterRoot activeInHierarchy=true"));
        }

        /// <summary>深度 0 はルートだけを表示します。</summary>
        [Test]
        public void ZeroDepthIncludesOnlyRoots()
        {
            var text = SceneHierarchyDumpText.Format(CreateHierarchy(), depth: 0);
            Assert.That(text, Is.EqualTo("scene=First" + Environment.NewLine
                + "Root activeInHierarchy=true" + Environment.NewLine + "LaterRoot activeInHierarchy=true"));
        }

        /// <summary>ノード上限をシーンごとにリセットせず、後続シーンで打ち切ります。</summary>
        [Test]
        public void MaximumNodesAppliesAcrossScenes()
        {
            const int MaximumNodes = 3;
            var dump = new SceneHierarchyDump
            {
                scenes = new[]
                {
                    new SceneHierarchyScene { name = "First", nodes = new[] { CreateNode(0, RootParentIndex, "FirstRoot"), CreateNode(1, 0, "Child") } },
                    new SceneHierarchyScene { name = "Second", nodes = new[] { CreateNode(0, RootParentIndex, "SecondRoot"), CreateNode(1, 0, "Omitted") } },
                },
            };
            var text = SceneHierarchyDumpText.Format(dump, maxNodes: MaximumNodes);
            Assert.That(text, Does.Contain("SecondRoot activeInHierarchy=true"));
            Assert.That(text, Does.Not.Contain("Omitted"));
            Assert.That(text, Does.EndWith("... maxNodes=3"));
        }

        /// <summary>非表示の親も activeInHierarchy に影響し、フィルタ後のノードだけを件数へ数えます。</summary>
        [Test]
        public void FilterPreservesInactiveAncestorAndCountsOnlyMatches()
        {
            const int MaximumNodes = 1;
            var dump = CreateHierarchy();
            dump.scenes[0].nodes[0].activeSelf = false;
            var text = SceneHierarchyDumpText.Format(dump, maxNodes: MaximumNodes, filter: "Child");
            Assert.That(text, Is.EqualTo("scene=First" + Environment.NewLine + "  Child activeInHierarchy=false"));
        }

        /// <summary>名前の部分一致を使い、親のパス名だけで子を一致させません。</summary>
        [Test]
        public void FilterMatchesNameInsteadOfPath()
        {
            var dump = CreateHierarchy();
            dump.scenes[0].nodes[1].path = "Root/Child";
            var text = SceneHierarchyDumpText.Format(dump, filter: "Root");
            Assert.That(text, Does.Contain("Root activeInHierarchy=true"));
            Assert.That(text, Does.Contain("LaterRoot activeInHierarchy=true"));
            Assert.That(text, Does.Not.Contain("Child"));
        }

        /// <summary>不正な制限値は階層を表示する前に拒否します。</summary>
        [TestCase(-1, 1)]
        [TestCase(0, 0)]
        [TestCase(0, -1)]
        public void InvalidLimitsAreRejected(int depth, int maxNodes)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SceneHierarchyDumpText.Format(CreateHierarchy(), depth, maxNodes));
        }

        private static SceneHierarchyDump CreateHierarchy()
        {
            return new SceneHierarchyDump
            {
                scenes = new[]
                {
                    new SceneHierarchyScene
                    {
                        name = "First",
                        nodes = new[]
                        {
                            CreateNode(0, RootParentIndex, "Root"), CreateNode(1, 0, "Child"), CreateNode(2, 1, "Grandchild"),
                            CreateNode(3, 2, "GreatGrandchild"), CreateNode(4, 3, "TooDeep"), CreateNode(5, RootParentIndex, "LaterRoot"),
                        },
                    },
                },
            };
        }

        private static SceneHierarchyNode CreateNode(int index, int parentIndex, string name)
        {
            return new SceneHierarchyNode { index = index, parentIndex = parentIndex, name = name, activeSelf = true };
        }

        /// <summary>既定深度はルート 0 から 3 までで、4 段目を含めません。</summary>
        [Test]
        public void DefaultDepthStopsAfterThirdDescendant()
        {
            var text = SceneHierarchyDumpText.Format(CreateHierarchy());
            Assert.That(text, Does.Contain("\n      GreatGrandchild activeInHierarchy=true"));
            Assert.That(text, Does.Not.Contain("TooDeep"));
        }
        /// <summary>既定の 200 件を超えたノードを応答へ含めません。</summary>
        [Test]
        public void DefaultMaximumNodesTruncatesAtTwoHundred()
        {
            const int ExpectedMaximumNodes = 200;
            var nodes = new SceneHierarchyNode[ExpectedMaximumNodes + 1];
            for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                nodes[nodeIndex] = CreateNode(nodeIndex, RootParentIndex, "Node" + nodeIndex);
            }

            var dump = new SceneHierarchyDump { scenes = new[] { new SceneHierarchyScene { name = "First", nodes = nodes } } };
            var text = SceneHierarchyDumpText.Format(dump);
            Assert.That(text, Does.Contain("Node199 activeInHierarchy=true"));
            Assert.That(text, Does.Not.Contain("Node200 activeInHierarchy"));
            Assert.That(text, Does.EndWith("... maxNodes=200"));
        }
    }
}
#endif
