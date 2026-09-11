#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace UniTestify
{
    /// <summary>HTTP 本文と認証・接続元の判断を、ソケットやシーンに依存しない形で検証します。</summary>
    internal static class AiHttpRequestParser
    {
        private const string BearerPrefix = "Bearer ";
        private const int BitsPerByte = 8;

        /// <summary>メールボックスと同じ op / args の要求へ本文を復元します。</summary>
        internal static AiCommandRequest Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new FormatException("HTTP 本文には JSON オブジェクトが必要です。");
            }

            AiJsonObject.Parse(body);
            return JsonUtility.FromJson<AiCommandRequest>(body);
        }

        /// <summary>空の設定やヘッダーを許可せず、Bearer トークンの完全一致を確認します。</summary>
        internal static bool IsTokenValid(string authorization, string token)
        {
            if (string.IsNullOrEmpty(token) || authorization == null
                || !authorization.StartsWith(BearerPrefix, StringComparison.Ordinal)
                || authorization.Length != BearerPrefix.Length + token.Length)
            {
                return false;
            }

            // 不一致の位置によって比較の走査回数を変えない。
            var difference = 0;
            for (var characterIndex = 0; characterIndex < token.Length; characterIndex++)
            {
                difference |= authorization[BearerPrefix.Length + characterIndex] ^ token[characterIndex];
            }

            return difference == 0;
        }

        /// <summary>IPv4 射影 IPv6 も正規化し、IPAddress.IsLoopback で接続元を判定します。</summary>
        internal static bool IsLoopback(IPAddress address)
        {
            return address != null && IPAddress.IsLoopback(Normalize(address));
        }

        /// <summary>ループバック、または明示許可されたローカルインターフェースのサブネットだけを通します。</summary>
        internal static bool IsAddressAllowed(IPAddress remoteAddress, bool allowLan, IPAddress localAddress, int prefixLength)
        {
            if (IsLoopback(remoteAddress))
            {
                return true;
            }

            if (!allowLan || remoteAddress == null || localAddress == null || IsLoopback(localAddress))
            {
                return false;
            }

            var remote = Normalize(remoteAddress);
            var local = Normalize(localAddress);
            if (remote.AddressFamily != local.AddressFamily || IsUnspecified(remote) || IsUnspecified(local))
            {
                return false;
            }

            if (remote.AddressFamily == AddressFamily.InterNetworkV6
                && remote.IsIPv6LinkLocal && remote.ScopeId != local.ScopeId)
            {
                return false;
            }

            return HasSamePrefix(remote.GetAddressBytes(), local.GetAddressBytes(), prefixLength);
        }

        /// <summary>連続したビットのサブネットマスクだけをプレフィックス長へ変換します。</summary>
        internal static int GetPrefixLength(IPAddress mask)
        {
            if (mask == null)
            {
                return 0;
            }

            var prefixLength = 0;
            var hasHostBits = false;
            foreach (var addressByte in mask.GetAddressBytes())
            {
                for (var bitIndex = BitsPerByte - 1; bitIndex >= 0; bitIndex--)
                {
                    var isNetworkBit = (addressByte & (1 << bitIndex)) != 0;
                    if (hasHostBits && isNetworkBit)
                    {
                        return 0;
                    }

                    hasHostBits |= !isNetworkBit;
                    prefixLength += isNetworkBit ? 1 : 0;
                }
            }

            return prefixLength;
        }

        private static IPAddress Normalize(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private static bool IsUnspecified(IPAddress address)
        {
            return address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        }

        private static bool HasSamePrefix(byte[] remote, byte[] local, int prefixLength)
        {
            if (prefixLength <= 0 || prefixLength > local.Length * BitsPerByte)
            {
                return false;
            }

            for (var addressIndex = 0; addressIndex < local.Length && prefixLength > 0; addressIndex++)
            {
                var comparedBits = Math.Min(BitsPerByte, prefixLength);
                var mask = byte.MaxValue << (BitsPerByte - comparedBits);
                if ((remote[addressIndex] & mask) != (local[addressIndex] & mask))
                {
                    return false;
                }

                prefixLength -= comparedBits;
            }

            return true;
        }
    }
}
#endif
