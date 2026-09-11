#if UNITY_EDITOR
using System;
using System.Net;
using NUnit.Framework;
using UnityEngine;

namespace UniTestify.Tests
{
    /// <summary>HTTP 要求・認証・接続元と設定上書きを、待受や PlayMode を使わず検証します。</summary>
    public sealed class AiHttpRequestParserTest
    {
        /// <summary>既存メールボックスの要求型と日本語の二重 JSON を維持します。</summary>
        [Test]
        public void BodyRestoresMailboxRequest()
        {
            var request = new AiCommandRequest { op = "agent.act", args = "{\"action\":{\"submit\":\"開始\"}}" };
            var restored = AiHttpRequestParser.Parse(JsonUtility.ToJson(request));
            Assert.That(restored.op, Is.EqualTo(request.op));
            Assert.That(restored.args, Is.EqualTo(request.args));
        }

        /// <summary>引数の省略は既存ディスパッチャの既定値解決へ委ねます。</summary>
        [Test]
        public void MissingArgumentsRemainOptional()
        {
            var request = AiHttpRequestParser.Parse("{\"op\":\"agent.observe\"}");
            Assert.That(request.op, Is.EqualTo("agent.observe"));
            Assert.That(string.IsNullOrEmpty(request.args), Is.True);
        }

        /// <summary>外側の JSON が壊れている要求をキューへ積ませません。</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("{")]
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("{\"op\":\"ping\",}")]
        [TestCase("{\"op\":\"ping\"} trailing")]
        public void InvalidBodyIsRejected(string body)
        {
            Assert.Throws<FormatException>(() => AiHttpRequestParser.Parse(body));
        }

        /// <summary>未指定・別方式・大文字小文字や前後の差異を認証成功としません。</summary>
        [TestCase("Bearer secret", "secret", true)]
        [TestCase(null, "secret", false)]
        [TestCase("", "secret", false)]
        [TestCase("Bearer ", "", false)]
        [TestCase("Bearer secret", null, false)]
        [TestCase("Basic secret", "secret", false)]
        [TestCase("bearer secret", "secret", false)]
        [TestCase("Bearer Secret", "secret", false)]
        [TestCase("Bearer secrex", "secret", false)]
        [TestCase("Bearer secret ", "secret", false)]
        [TestCase("Bearer  secret", "secret", false)]
        public void BearerTokenRequiresExactMatch(string authorization, string token, bool expected)
        {
            Assert.That(AiHttpRequestParser.IsTokenValid(authorization, token), Is.EqualTo(expected));
        }

        /// <summary>IPv4・IPv6・IPv4 射影を区別せず、ループバックだけを既定で許可します。</summary>
        [TestCase("127.0.0.1", true)]
        [TestCase("127.2.3.4", true)]
        [TestCase("::1", true)]
        [TestCase("::ffff:127.0.0.1", true)]
        [TestCase("192.168.1.20", false)]
        [TestCase("::ffff:192.168.1.20", false)]
        [TestCase("10.0.0.1", false)]
        [TestCase("2001:db8::1", false)]
        [TestCase("0.0.0.0", false)]
        [TestCase("::", false)]
        public void LoopbackPolicyRejectsRemoteAddresses(string address, bool expected)
        {
            var remoteAddress = IPAddress.Parse(address);
            Assert.That(AiHttpRequestParser.IsLoopback(remoteAddress), Is.EqualTo(expected));
            Assert.That(AiHttpRequestParser.IsAddressAllowed(remoteAddress, false, null, 0), Is.EqualTo(expected));
        }

        /// <summary>接続元を取得できない要求は LAN 許可時も拒否します。</summary>
        [Test]
        public void MissingAddressIsRejected()
        {
            Assert.That(AiHttpRequestParser.IsLoopback(null), Is.False);
            Assert.That(AiHttpRequestParser.IsAddressAllowed(null, true, IPAddress.Loopback, 0), Is.False);
        }

        /// <summary>LAN 許可を全ネットワーク許可へ広げず、実際のサブネットと IPv6 スコープを照合します。</summary>
        [TestCase("192.168.1.20", "192.168.1.10", 24, true)]
        [TestCase("192.168.2.20", "192.168.1.10", 24, false)]
        [TestCase("10.0.0.20", "192.168.1.10", 24, false)]
        [TestCase("192.168.1.129", "192.168.1.10", 25, false)]
        [TestCase("192.168.1.127", "192.168.1.10", 25, true)]
        [TestCase("192.168.1.10", "192.168.1.10", 32, true)]
        [TestCase("192.168.1.20", "192.168.1.10", 0, false)]
        [TestCase("192.168.1.20", "192.168.1.10", 33, false)]
        [TestCase("192.168.1.20", "0.0.0.0", 24, false)]
        [TestCase("::ffff:192.168.1.20", "192.168.1.10", 24, true)]
        [TestCase("2001:db8:1::20", "2001:db8:1::10", 64, true)]
        [TestCase("2001:db8:2::20", "2001:db8:1::10", 64, false)]
        [TestCase("fe80::20%3", "fe80::10%3", 64, true)]
        [TestCase("fe80::20%4", "fe80::10%3", 64, false)]
        [TestCase("192.168.1.20", "2001:db8::10", 64, false)]
        public void LanPermissionRequiresSameSubnet(string remote, string local, int prefixLength, bool expected)
        {
            Assert.That(AiHttpRequestParser.IsAddressAllowed(IPAddress.Parse(remote), true,
                IPAddress.Parse(local), prefixLength), Is.EqualTo(expected));
        }

        /// <summary>Unity の未実装 API を使わず、実際の IPv4 マスクを判定へ渡せます。</summary>
        [TestCase("255.255.255.0", 24)]
        [TestCase("255.255.255.128", 25)]
        [TestCase("255.255.255.255", 32)]
        [TestCase("255.255.0.0", 16)]
        [TestCase("0.0.0.0", 0)]
        [TestCase("255.0.255.0", 0)]
        public void SubnetMaskRequiresContiguousNetworkBits(string mask, int expected)
        {
            Assert.That(AiHttpRequestParser.GetPrefixLength(IPAddress.Parse(mask)), Is.EqualTo(expected));
        }

        /// <summary>初期設定は HTTP と LAN が無効で、空トークンだけは起動ごとに補います。</summary>
        [Test]
        public void MissingConfigurationUsesDisabledDefaults()
        {
            var configuration = AiHttpConfiguration.Resolve(null, null);
            Assert.That(configuration.httpEnabled, Is.False);
            Assert.That(configuration.httpAllowLan, Is.False);
            Assert.That(configuration.httpPort, Is.EqualTo(UniTestifySettings.DefaultHttpPort));
            Assert.That(configuration.httpToken, Is.Not.Empty);
            Assert.That(AiHttpConfiguration.Resolve(null, null).httpToken, Is.Not.EqualTo(configuration.httpToken));
        }

        /// <summary>外部ファイルの部分指定が省略項目や元の設定アセットを壊しません。</summary>
        [Test]
        public void OverridePreservesUnspecifiedBuildSettings()
        {
            var settings = ScriptableObject.CreateInstance<UniTestifySettings>();
            try
            {
                settings.httpEnabled = true;
                settings.httpAllowLan = true;
                settings.httpToken = "build-token";
                var configuration = AiHttpConfiguration.Resolve(settings, "{\"httpPort\":0,\"httpAllowLan\":false}");
                Assert.That(configuration.httpEnabled, Is.True);
                Assert.That(configuration.httpPort, Is.Zero);
                Assert.That(configuration.httpAllowLan, Is.False);
                Assert.That(configuration.httpToken, Is.EqualTo(settings.httpToken));
                Assert.That(settings.httpPort, Is.EqualTo(UniTestifySettings.DefaultHttpPort));
                Assert.That(settings.httpAllowLan, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        /// <summary>全フィールドの上書きと明示した無効化を反映します。</summary>
        [Test]
        public void OverrideReplacesAllHttpFields()
        {
            var settings = ScriptableObject.CreateInstance<UniTestifySettings>();
            try
            {
                settings.httpEnabled = true;
                var configuration = AiHttpConfiguration.Resolve(settings,
                    "{\"httpEnabled\":false,\"httpPort\":65535,\"httpToken\":\"device-token\",\"httpAllowLan\":true}");
                Assert.That(configuration.httpEnabled, Is.False);
                Assert.That(configuration.httpPort, Is.EqualTo(IPEndPoint.MaxPort));
                Assert.That(configuration.httpToken, Is.EqualTo("device-token"));
                Assert.That(configuration.httpAllowLan, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        /// <summary>型違いとポート範囲外を JsonUtility の暗黙変換へ任せません。</summary>
        [TestCase("{\"httpEnabled\":1}")]
        [TestCase("{\"httpAllowLan\":\"true\"}")]
        [TestCase("{\"httpPort\":-1}")]
        [TestCase("{\"httpPort\":65536}")]
        [TestCase("{\"httpPort\":1.5}")]
        [TestCase("{\"httpPort\":\"7910\"}")]
        [TestCase("{\"httpToken\":null}")]
        [TestCase("{\"httpToken\":123}")]
        [TestCase("{\"httpToken\":\"bad\\r\\ntoken\"}")]
        public void InvalidConfigurationIsRejected(string json)
        {
            Assert.Throws<ArgumentException>(() => AiHttpConfiguration.Resolve(null, json));
        }
    }
}
#endif
