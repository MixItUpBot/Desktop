using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using MixItUp.Base.Util;

namespace MixItUp.Base.Web
{
    /// <summary>
    /// Handles web socket communication for client connections.
    /// </summary>
    public abstract class ClientWebSocketBase : WebSocketBase
    {
        /// <summary>
        /// Locking semaphore to prevent connect/disconnect races.
        /// </summary>
        private readonly SemaphoreSlim connectionSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Lock object for accessing websocket state fields.
        /// </summary>
        private readonly object webSocketStateLock = new object();

        /// <summary>
        /// The cancellation token source for the active receive loop.
        /// </summary>
        private CancellationTokenSource receiveCancellationTokenSource;

        /// <summary>
        /// The active receive loop task.
        /// </summary>
        private Task receiveTask;

        /// <summary>
        /// The web socket connection.
        /// </summary>
        protected new ClientWebSocket webSocket;

        /// <summary>
        /// Gets the cancellation token to use for receiving.
        /// </summary>
        protected override CancellationToken ReceiveCancellationToken
        {
            get { return this.receiveCancellationTokenSource?.Token ?? CancellationToken.None; }
        }

        /// <summary>
        /// Connects the web socket to the server.
        /// </summary>
        /// <param name="endpoint">The endpoint to connect to</param>
        /// <returns>Whether the connection was successful</returns>
        public virtual async Task<bool> Connect(string endpoint)
        {
            ClientWebSocket socket = null;
            await this.connectionSemaphore.WaitAsync();
            try
            {
                await this.DisconnectInternal(WebSocketCloseStatus.NormalClosure, waitForReceiveTask: true);

                socket = this.CreateWebSocket();
                await WebSocketConnectionFactory.ConnectAsync(socket, new Uri(endpoint), CancellationToken.None);

                CancellationTokenSource cts = new CancellationTokenSource();
                lock (this.webSocketStateLock)
                {
                    this.webSocket = socket;
                    this.SetWebSocket(socket);

                    this.receiveCancellationTokenSource = cts;
                    this.receiveTask = Task.Run(() => this.Receive());
                }
                socket = null;

                return IsOpen();
            }
            catch (Exception ex)
            {
                socket?.Dispose();
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
        public override async Task Disconnect(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure)
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
        /// Sends the packet of data.
        /// </summary>
        /// <param name="buffer">The buffer to send</param>
        /// <returns>An awaitable task</returns>
        protected override async Task SendInternal(byte[] buffer)
        {
            ClientWebSocket socket = this.webSocket;
            if (socket != null && socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Creates the web socket for connecting with.
        /// </summary>
        /// <returns>The web socket to use</returns>
        protected virtual ClientWebSocket CreateWebSocket() { return new ClientWebSocket(); }

        private async Task DisconnectInternal(WebSocketCloseStatus closeStatus, bool waitForReceiveTask)
        {
            ClientWebSocket socketToClose = null;
            CancellationTokenSource cancellationTokenSourceToCancel = null;
            Task receiveTaskToWait = null;

            lock (this.webSocketStateLock)
            {
                socketToClose = this.webSocket;
                cancellationTokenSourceToCancel = this.receiveCancellationTokenSource;
                receiveTaskToWait = this.receiveTask;

                this.webSocket = null;
                this.SetWebSocket(null);

                this.receiveCancellationTokenSource = null;
                this.receiveTask = null;
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

            if (waitForReceiveTask && receiveTaskToWait != null && !receiveTaskToWait.IsCompleted && Task.CurrentId != receiveTaskToWait.Id)
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
    }
}
