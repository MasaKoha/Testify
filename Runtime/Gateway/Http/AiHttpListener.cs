#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UniTestify
{
    /// <summary>HTTP の入出力をワーカーで完結させ、Unity の実行待ちはキューで橋渡しします。</summary>
    internal sealed class AiHttpListener : IDisposable
    {
        private const int AutomaticPortAttempts = 5;
        private const string OperationPath = "/op";
        private readonly HttpListener _listener;
        private readonly AiHttpConfiguration _configuration;
        private readonly ConcurrentQueue<AiHttpExchange> _requests = new ConcurrentQueue<AiHttpExchange>();
        private readonly object _lifetimeGate = new object();
        private AiHttpExchange _currentExchange;
        private volatile bool _isStopped;
        private volatile Exception _failure;

        /// <summary>実際に待受を開始できたポートです。</summary>
        internal int Port { get; }

        /// <summary>受付ループを継続できない障害をメインスレッドへ渡します。</summary>
        internal Exception Failure => _failure;

        /// <summary>指定ポートまたは空きポートで待受を確保します。</summary>
        internal AiHttpListener(AiHttpConfiguration configuration)
        {
            _configuration = configuration;
            _listener = OpenListener(configuration.httpPort, out var actualPort);
            Port = actualPort;
        }

        /// <summary>接続情報の公開後に一度だけ呼び、Unity の同期コンテキストを持ち込まず受付を開始します。</summary>
        internal void Start()
        {
            _ = Task.Run(ReceiveAsync);
        }

        /// <summary>メインスレッドが実行できる要求を受付順に取り出します。</summary>
        internal bool TryDequeue(out AiHttpExchange exchange)
        {
            return _requests.TryDequeue(out exchange);
        }

        /// <summary>応答待ちとソケット待ちを解除し、破棄後の要求公開を防ぎます。</summary>
        public void Dispose()
        {
            lock (_lifetimeGate)
            {
                if (_isStopped)
                {
                    return;
                }

                _isStopped = true;
                _currentExchange?.Complete(JsonUtility.ToJson(new AiCommandResponse { error = "server stopped" }));
                _listener.Close();
            }
        }

        private static HttpListener OpenListener(int requestedPort, out int actualPort)
        {
            for (var attempt = 0; attempt < AutomaticPortAttempts; attempt++)
            {
                actualPort = requestedPort == IPEndPoint.MinPort ? FindAvailablePort() : requestedPort;
                var listener = new HttpListener();
                try
                {
                    listener.Prefixes.Add($"http://+:{actualPort}/");
                    listener.Start();
                    return listener;
                }
                catch (Exception exception)
                {
                    listener.Close();
                    // HttpListener はポート 0 の実ポートを公開しないため、仮確保後の競合時だけ再選択する。
                    if (requestedPort != IPEndPoint.MinPort || !(exception is HttpListenerException)
                        || attempt == AutomaticPortAttempts - 1)
                    {
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("HTTP の空きポートを確保できませんでした。");
        }

        private static int FindAvailablePort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        private async Task ReceiveAsync()
        {
            try
            {
                while (!_isStopped)
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    await HandleContextAsync(context).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (!_isStopped)
                {
                    _failure = exception;
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            try
            {
                using (var response = context.Response)
                {
                    await RespondAsync(context.Request, response).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is HttpListenerException
                || exception is ObjectDisposedException)
            {
                // クライアントの切断や終了時のソケット破棄で、次の要求の受付まで止めない。
            }
        }

        private async Task RespondAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (!IsAddressAllowed(request.RemoteEndPoint?.Address))
            {
                await WriteErrorAsync(response, HttpStatusCode.Forbidden, "connection forbidden").ConfigureAwait(false);
                return;
            }

            if (!AiHttpRequestParser.IsTokenValid(request.Headers["Authorization"], _configuration.httpToken))
            {
                response.Headers["WWW-Authenticate"] = "Bearer";
                await WriteErrorAsync(response, HttpStatusCode.Unauthorized, "invalid bearer token").ConfigureAwait(false);
                return;
            }

            if (request.Url == null || request.Url.AbsolutePath != OperationPath)
            {
                await WriteErrorAsync(response, HttpStatusCode.NotFound, "unknown endpoint").ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod != "POST")
            {
                response.Headers["Allow"] = "POST";
                await WriteErrorAsync(response, HttpStatusCode.MethodNotAllowed, "POST required").ConfigureAwait(false);
                return;
            }

            await DispatchAsync(request, response).ConfigureAwait(false);
        }

        private async Task DispatchAsync(HttpListenerRequest request, HttpListenerResponse response)
        {
            AiCommandRequest command;
            try
            {
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                {
                    command = AiHttpRequestParser.Parse(await reader.ReadToEndAsync().ConfigureAwait(false));
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            {
                await WriteErrorAsync(response, HttpStatusCode.BadRequest, exception.Message).ConfigureAwait(false);
                return;
            }

            var exchange = new AiHttpExchange(command);
            lock (_lifetimeGate)
            {
                if (_isStopped)
                {
                    return;
                }

                _currentExchange = exchange;
                _requests.Enqueue(exchange);
            }

            // 応答完了まで次を受け付けず、コルーチンの行動が要求間で交錯しないようにする。
            var responseJson = await exchange.Response.ConfigureAwait(false);
            await WriteJsonAsync(response, HttpStatusCode.OK, responseJson).ConfigureAwait(false);
        }

        private bool IsAddressAllowed(IPAddress remoteAddress)
        {
            if (AiHttpRequestParser.IsLoopback(remoteAddress))
            {
                return true;
            }

            if (!_configuration.httpAllowLan || remoteAddress == null)
            {
                return false;
            }

            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && network.NetworkInterfaceType != NetworkInterfaceType.Tunnel && network.NetworkInterfaceType != NetworkInterfaceType.Ppp
                    && IsOnNetwork(remoteAddress, network))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOnNetwork(IPAddress remoteAddress, NetworkInterface network)
        {
            foreach (var address in network.GetIPProperties().UnicastAddresses)
            {
                if (AiHttpRequestParser.IsAddressAllowed(remoteAddress, true, address.Address, GetPrefixLength(address)))
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetPrefixLength(UnicastIPAddressInformation address)
        {
            if (address.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                // Unity 6 同梱 Mono の PrefixLength は未実装なので、IPv4 はマスクから求める。
                return AiHttpRequestParser.GetPrefixLength(address.IPv4Mask);
            }

            try
            {
                return address.PrefixLength;
            }
            catch (NotImplementedException)
            {
                // マスクが得られない環境で /64 と推測すると、同一 LAN の制限を広げ得る。
                return 0;
            }
            catch (PlatformNotSupportedException)
            {
                return 0;
            }
        }

        private static Task WriteErrorAsync(HttpListenerResponse response, HttpStatusCode status, string error)
        {
            return WriteJsonAsync(response, status, JsonUtility.ToJson(new AiCommandResponse { error = error }));
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.KeepAlive = false;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
    }
}
#endif
