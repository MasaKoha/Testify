#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UniTestify.Editor;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>Editor API を呼ばずに要求解釈と拒否応答の契約を検証します。</summary>
    public sealed class EditorControlRequestTest
    {
        /// <summary>メニューパスの空白・日本語・引用符を失わず復元します。</summary>
        [Test]
        public void FromJsonPreservesMenuArgument()
        {
            const string MenuPath = "UniTestify/日本語 の項目/\"確認\"";
            var original = new EditorControlRequest { op = "menu", arg = MenuPath };
            var restored = EditorControlRequest.FromJson(JsonUtility.ToJson(original));

            Assert.That(restored.op, Is.EqualTo("menu"));
            Assert.That(restored.arg, Is.EqualTo(MenuPath));
            Assert.That(restored.Validate(false), Is.Null);
        }

        /// <summary>引数のない操作では arg を省略できます。</summary>
        [Test]
        public void MissingArgumentDefaultsToEmpty()
        {
            var request = EditorControlRequest.FromJson("{\"op\":\"status\"}");

            Assert.That(request.arg, Is.Empty);
            Assert.That(request.Validate(false), Is.Null);
        }

        /// <summary>コンパイル中は状態照会だけを受け付けます。</summary>
        [TestCase("status")]
        [TestCase("play")]
        public void CompilationRejectsEveryOperationExceptStatus(string operation)
        {
            var request = new EditorControlRequest { op = operation, arg = "Window/General/Console" };
            var response = request.Validate(true);
            if (operation == "status")
            {
                Assert.That(response, Is.Null);
                return;
            }

            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Does.Contain("コンパイル中"));
        }

        /// <summary>未知・省略・大文字違いの op を成功扱いにしません。</summary>
        [TestCase("{\"op\":\"unknown\"}", "unknown")]
        [TestCase("{}", "")]
        public void UnknownOperationReturnsFailure(string json, string operation)
        {
            var response = EditorControlRequest.FromJson(json).Validate(false);

            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Is.EqualTo($"未知の op です: {operation}"));
        }

        /// <summary>メニューパスが欠けた要求を実行前に拒否します。</summary>
        [TestCase("{\"op\":\"menu\"}")]
        [TestCase("{\"op\":\"menu\",\"arg\":\"  \"}")]
        public void MenuRequiresArgument(string json)
        {
            var response = EditorControlRequest.FromJson(json).Validate(false);

            Assert.That(response.ok, Is.False);
            Assert.That(response.message, Does.Contain("メニューパス"));
        }

        /// <summary>壊れた JSON や文字列以外の op / arg を補正して実行しません。</summary>
        [TestCase("{")]
        [TestCase("{\"op\":null}")]
        [TestCase("{\"op\":1}")]
        [TestCase("{\"op\":\"menu\",\"arg\":{}}")]
        [TestCase("{\"op\":\"menu\",\"arg\":null}")]
        public void InvalidJsonIsRejected(string json)
        {
            Assert.Throws<FormatException>(() => EditorControlRequest.FromJson(json));
        }

        /// <summary>成功・失敗ともに応答のフィールドを ok と message に限定します。</summary>
        [TestCase(false)]
        public void ResponseContainsOnlyContractFields(bool succeeded)
        {
            const string Message = "日本語の応答\n\"次行\"";
            var response = succeeded ? EditorControlResponse.Success(Message) : EditorControlResponse.Failure(Message);
            var json = response.ToJson();
            var members = AiJsonObject.Parse(json);
            var restored = JsonUtility.FromJson<EditorControlResponse>(json);

            Assert.That(members.Keys, Is.EquivalentTo(new[] { "ok", "message" }));
            Assert.That(restored.ok, Is.EqualTo(succeeded));
            Assert.That(restored.message, Is.EqualTo(Message));
        }
    }
}
#endif
