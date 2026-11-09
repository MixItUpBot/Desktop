using MixItUp.Base.Util;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public class AdvancedClientWebSocket
    {
        /// <summary>
        /// The base buffer size for websocket buffers.
        /// </summary>
        protected const int BUFFER_SIZE = 1000000;

        /// <summary>
        /// Invoked when a packet is sent.
        /// </summary>
        public event EventHandler<string> PacketSent = delegate { };
        /// <summary>
        /// Invoked when a text packet is received.
        /// </summary>
        public event EventHandler<string> PacketReceived = delegate { };
        /// <summary>
        /// Invoked when an unexpected disconnection occurs.
        /// </summary>
        public event EventHandler<WebSocketCloseStatus> Disconnected = delegate { };

        /// <summary>
        /// Locking semaphore to prevent clashing packet sends.
        /// </summary>
        protected readonly SemaphoreSlim webSocketSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Locking semaphore to prevent connect/disconnect races.
        /// </summary>
        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Lock object for accessing websocket state fields.
        /// </summary>
        private readonly object webSocketStateLock = new object();

        /// <summary>
        /// The web socket connection.
        /// </summary>
        protected ClientWebSocket webSocket;

        /// <summary>
        /// The HTTP status code returned during the most recent failed websocket connect, if available.
        /// </summary>
        public int? LastConnectHttpStatusCode { get; private set; }

        /// <summary>
        /// The cancellation token source for the active receive loop.
        /// </summary>
        private CancellationTokenSource receiveCancellationTokenSource;

        /// <summary>
        /// The active receive loop task.
        /// </summary>
        private Task receiveTask;

        /// <summary>
        /// Connects the web socket to the server.
        /// </summary>
        /// <param name="endpoint">The endpoint to connect to</param>
        /// <returns>Whether the connection was successful</returns>
        public virtual async Task<bool> Connect(string endpoint, CancellationToken cancellationToken)
        {
            ClientWebSocket socket = null;
            await this.connectionSemaphore.WaitAsync(cancellationToken);
            try
            {
                this.LastConnectHttpStatusCode = null;

                await this.DisconnectInternal(WebSocketCloseStatus.NormalClosure, waitForReceiveTask: true);

                socket = new ClientWebSocket();

                await WebSocketConnectionFactory.ConnectAsync(socket, new Uri(endpoint), cancellationToken);

                CancellationTokenSource cts = new CancellationTokenSource();
                lock (this.webSocketStateLock)
                {
                    this.webSocket = socket;
                    this.receiveCancellationTokenSource = cts;
                    ClientWebSocket connectedSocket = socket;
                    this.receiveTask = Task.Run(() => this.Receive(connectedSocket, cts.Token));
                }

                bool connected = socket.State == WebSocketState.Open;
                socket = null;
                return connected;
            }
            catch (Exception ex)
            {
                socket?.Dispose();
                this.LastConnectHttpStatusCode = this.ExtractHttpStatusCode(ex);

                await this.DisconnectInternal(WebSocketCloseStatus.NormalClosure, waitForReceiveTask: true);
                if (ex is WebSocketException websocketEx)
                {
                    if (websocketEx.InnerException is WebException webException &&
                        webException.Response is HttpWebResponse httpWebResponse)
                    {
                        using (StreamReader reader = new StreamReader(httpWebResponse.GetResponseStream()))
                        {
                            string responseString = reader.ReadToEnd();
                            throw new WebSocketException(string.Format("{0} - {1} - {2}", httpWebResponse.StatusCode, httpWebResponse.StatusDescription, responseString), ex);
                        }
                    }

                    if (websocketEx.InnerException is HttpRequestException httpRequestEx &&
                        httpRequestEx.StatusCode != null)
                    {
                        throw new WebSocketException(string.Format("{0} - {1}", (int)httpRequestEx.StatusCode.Value, httpRequestEx.Message), ex);
                    }
                }
                throw;
            }
            finally
            {
                this.connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Disconnects the web socket.
        /// </summary>
        /// <param name="closeStatus">Optional status to send to partner web socket as to why the web socket is being closed</param>
        /// <returns>A task for the closing of the web socket</returns>
        public async Task Disconnect(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure)
        {
            await this.connectionSemaphore.WaitAsync();
            try
            {
                await this.DisconnectInternal(closeStatus, waitForReceiveTask: true);
            }
            finally
            {
                this.connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Sends a JSON-serializable packet to the connected web socket.
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <returns>A task for the sending of the packet</returns>
        public async Task Send(object packet)
        {
            await this.Send(JSONSerializerHelper.SerializeToString(packet));
        }

        /// <summary>
        /// Sends a text packet to the connected web socket.
        /// </summary>
        /// <param name="packet">The text packet to send</param>
        /// <returns>A task for the sending of the packet</returns>
        public async Task Send(string packet)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(packet);

            await this.webSocketSemaphore.WaitAsync();
            try
            {
                ClientWebSocket socket = this.webSocket;
                if (socket != null && socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            finally
            {
                this.webSocketSemaphore.Release();
            }

            this.PacketSent?.Invoke(this, packet);
        }

        /// <summary>
        /// Gets whether the web socket is currently open.
        /// </summary>
        /// <returns>Whether the web socket is currently open</returns>
        public bool IsOpen() { return this.GetState() == WebSocketState.Open; }

        /// <summary>
        /// Gets the current state of the web socket.
        /// </summary>
        /// <returns>The current state of the web socket</returns>
        public WebSocketState GetState()
        {
            ClientWebSocket socket = this.webSocket;
            if (socket != null)
            {
                return socket.State;
            }
            return WebSocketState.Closed;
        }

        /// <summary>
        /// Handles all receiving &amp; processing of packets for a specific socket connection.
        /// </summary>
        /// <param name="socket">The socket to receive from</param>
        /// <param name="cancellationToken">Cancellation token for this receive loop</param>
        /// <returns>An awaitable task with the close status of the web socket connection</returns>
        protected virtual async Task<WebSocketCloseStatus> Receive(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            string jsonBuffer = string.Empty;
            byte[] buffer = new byte[AdvancedClientWebSocket.BUFFER_SIZE];
            ArraySegment<byte> arrayBuffer = new ArraySegment<byte>(buffer);

            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure;

            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    try
                    {
                        Array.Clear(buffer, 0, buffer.Length);
                        WebSocketReceiveResult result = await socket.ReceiveAsync(arrayBuffer, cancellationToken);

                        if (result != null)
                        {
                            if (result.MessageType == WebSocketMessageType.Close || (result.CloseStatus != null && result.CloseStatus.GetValueOrDefault() != WebSocketCloseStatus.Empty))
                            {
                                closeStatus = result.CloseStatus.GetValueOrDefault();
                                break;
                            }
                            else if (result.MessageType == WebSocketMessageType.Text)
                            {
                                jsonBuffer += Encoding.UTF8.GetString(buffer, 0, result.Count);
                                if (result.EndOfMessage)
                                {
                                    this.PacketReceived?.Invoke(this, jsonBuffer);
                                    jsonBuffer = string.Empty;
                                }
                            }
                            else
                            {
                                Logger.Log("Unsupported packet received");
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        closeStatus = cancellationToken.IsCancellationRequested ? WebSocketCloseStatus.NormalClosure : WebSocketCloseStatus.InternalServerError;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        closeStatus = cancellationToken.IsCancellationRequested ? WebSocketCloseStatus.NormalClosure : WebSocketCloseStatus.InternalServerError;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        closeStatus = WebSocketCloseStatus.InternalServerError;
                        jsonBuffer = string.Empty;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                closeStatus = WebSocketCloseStatus.InternalServerError;
            }

            if (!cancellationToken.IsCancellationRequested &&
                closeStatus == WebSocketCloseStatus.NormalClosure &&
                socket.State == WebSocketState.Aborted)
            {
                closeStatus = WebSocketCloseStatus.InternalServerError;
            }

            await this.DisconnectInternal(closeStatus, waitForReceiveTask: false, expectedWebSocket: socket);
            if (closeStatus != WebSocketCloseStatus.NormalClosure)
            {
                this.Disconnected?.Invoke(this, closeStatus);
            }

            return closeStatus;
        }

        private async Task DisconnectInternal(WebSocketCloseStatus closeStatus, bool waitForReceiveTask, ClientWebSocket expectedWebSocket = null)
        {
            ClientWebSocket socketToClose = null;
            CancellationTokenSource cancellationTokenSourceToCancel = null;
            Task receiveTaskToWait = null;

            lock (this.webSocketStateLock)
            {
                if (expectedWebSocket != null && this.webSocket != null && !ReferenceEquals(this.webSocket, expectedWebSocket))
                {
                    socketToClose = expectedWebSocket;
                }
                else
                {
                    socketToClose = this.webSocket;
                    cancellationTokenSourceToCancel = this.receiveCancellationTokenSource;
                    receiveTaskToWait = this.receiveTask;

                    this.webSocket = null;
                    this.receiveCancellationTokenSource = null;
                    this.receiveTask = null;
                }
            }

            if (cancellationTokenSourceToCancel != null)
            {
                try { cancellationTokenSourceToCancel.Cancel(); }
                catch { }
                cancellationTokenSourceToCancel.Dispose();
            }

            if (socketToClose != null)
            {
                try
                {
                    if (socketToClose.State == WebSocketState.Open || socketToClose.State == WebSocketState.CloseReceived)
                    {
                        await socketToClose.CloseAsync(closeStatus, string.Empty, CancellationToken.None);
                    }
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                catch (Exception ex) { Logger.Log(ex); }
                finally
                {
                    socketToClose.Dispose();
                }
            }

            if (waitForReceiveTask && receiveTaskToWait != null && !receiveTaskToWait.IsCompleted)
            {
                try
                {
                    Task completedTask = await Task.WhenAny(receiveTaskToWait, Task.Delay(2000));
                    if (!ReferenceEquals(completedTask, receiveTaskToWait))
                    {
                        Logger.Log(LogLevel.Debug, "Timed out waiting for websocket receive loop to stop within 2 seconds");
                    }
                }
                catch { }
            }
        }

        private int? ExtractHttpStatusCode(Exception ex)
        {
            if (ex is WebSocketException websocketException)
            {
                if (websocketException.InnerException is WebException webException &&
                    webException.Response is HttpWebResponse response)
                {
                    return (int)response.StatusCode;
                }

                if (websocketException.InnerException is HttpRequestException httpRequestException &&
                    httpRequestException.StatusCode != null)
                {
                    return (int)httpRequestException.StatusCode.Value;
                }
            }

            return null;
        }
    }
}
