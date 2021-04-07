﻿using System;
using System.Net;
using System.Diagnostics;

namespace Pomelo.Net.Gateway.Tunnel
{
    public class PacketTunnelContext : IDisposable
    {
        public Guid ConnectionId { get; private set; }
        public string Identifier { get; private set; }
        public IPEndPoint LeftEndpoint { get; set; }
        public IPEndPoint RightEndpoint { get; set; }
        public PomeloUdpClient Client { get; set; } // Not used for server
        public DateTime LastActionTimeUtc { get; set; }

        internal PacketTunnelContext(Guid connectionId, string identifier)
        {
            ConnectionId = connectionId;
            Identifier = identifier;
        }

        public void Dispose()
        {
            Client?.Dispose();
            Client = null;
        }
    }
}