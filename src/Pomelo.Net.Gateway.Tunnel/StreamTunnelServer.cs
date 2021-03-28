﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Pomelo.Net.Gateway.Association.Token;
using Pomelo.Net.Gateway.Router;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class StreamTunnelServer : IDisposable
    {
        private TcpListener server;
        private StreamTunnelContextFactory streamTunnelContextFactory;
        private ITokenValidator tokenValidator;
        private IServiceProvider services;

        public StreamTunnelServer(
            IPEndPoint endpoint, 
            StreamTunnelContextFactory streamTunnelContextFactory, 
            ITokenValidator tokenValidator,
            IServiceProvider services)
        {
            server = new TcpListener(endpoint);
            this.streamTunnelContextFactory = streamTunnelContextFactory;
            this.tokenValidator = tokenValidator;
            this.services = services;
        }

        public void Start()
        {
            server.Start();
            StartAcceptAsync();
        }

        private async ValueTask StartAcceptAsync()
        {
            while (true)
            {
                var client = await server.AcceptTcpClientAsync();
                HandleClientAcceptAsync(client);
            }
        }

        private async ValueTask HandleClientAcceptAsync(TcpClient client)
        {
            var stream = client.GetStream();
            Guid connectionId = default;
            using (var authenticationBuffer = MemoryPool<byte>.Shared.Rent(8 + 16))
            {
                try
                {
                    var _authenticationBuffer = authenticationBuffer.Memory.Slice(0, 8 + 16);
                    await stream.ReadExAsync(_authenticationBuffer);
                    var token = BitConverter.ToInt64(_authenticationBuffer.Slice(0, 8).Span);
                    connectionId = new Guid(_authenticationBuffer.Slice(8, 16).Span);
                    var context = streamTunnelContextFactory.GetContextById(connectionId);
                    var result = await tokenValidator.ValidateAsync(token, context.UserIdentifier);
                    if (result)
                    {
                        _authenticationBuffer.Span[0] = 0x00;
                        await stream.WriteAsync(_authenticationBuffer.Slice(0, 1));
                    }
                    else
                    {
                        _authenticationBuffer.Span[0] = 0x01;
                        await stream.WriteAsync(_authenticationBuffer.Slice(0, 1));
                        client.Close();
                        client.Dispose();
                        return;
                    }
                    context.LeftClient = client;

                    // Forward header
                    await context.Tunnel.ForwardAsync(context.GetHeaderStream(), context.RightClient.GetStream());
                    context.DestroyHeaderBuffer();

                    // Start tunneling
                    await Task.WhenAll(new[]
                    {
                        context.Tunnel.ForwardAsync(context.LeftClient.GetStream(), context.RightClient.GetStream()).AsTask(),
                        context.Tunnel.BackwardAsync(context.RightClient.GetStream(), context.LeftClient.GetStream()).AsTask()
                    });
                }
                finally
                {
                    client.Close();
                    client.Dispose();

                    if (connectionId != default)
                    {
                        streamTunnelContextFactory.Delete(connectionId);
                    }
                }
            }
        }

        public void Dispose()
        {
            server?.Stop();
        }
    }
}