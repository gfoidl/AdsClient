﻿using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Viscon.Communication.Ads.Internal;

namespace Viscon.Communication.Ads
{
    internal class AmsSocket : IAmsSocket, IDisposable
    {
        public AmsSocket(string host, int port = 48898)
        {
            Host = host;
            Port = port;

            TcpClient = new TcpClient { NoDelay = true };
        }

        public TcpClient TcpClient { get; }
        public Socket Socket => TcpClient.Client;

        public string Host { get; }
        public int Port { get; }

        /// <inheritdoc cref="System.Net.Sockets.TcpClient.Connected"/>
        public bool Connected => TcpClient.Connected;

        private AmsSocketConnection connection;

        public void Close()
        {
            connection?.Close();
        }

        public async Task ConnectAsync(IIncomingMessageHandler messageHandler, CancellationToken cancellationToken = default)
        {
            if (connection is not null) throw new InvalidOperationException("Connection was already established.");

            using (cancellationToken.Register(state => ((Socket)state).Close(), Socket))
            {
                try
                {
                    await TcpClient.ConnectAsync(Host, Port).ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    // The exception handling is quite generic, but exceptions thrown differ across target frameworks.
                    // (See https://stackoverflow.com/a/66656805/1085457)
                    // This is probably not something to worry about, since apparently cancellation was requested anyway.
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            connection = new AmsSocketConnection(TcpClient.Client, messageHandler);
        }

        public async Task SendAsync(byte[] message)
        {
            using var args = new SocketAsyncEventArgs();
            args.SetBuffer(message, 0, message.Length);

            await Socket.SendAsync(new SocketAwaitable(args));
        }

        public async Task SendAsync(ArraySegment<byte> buffer)
        {
            using var args = new SocketAsyncEventArgs();
            args.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            await Socket.SendAsync(new SocketAwaitable(args));
        }

        void IDisposable.Dispose()
        {
            TcpClient?.Dispose();
        }
    }
}
