﻿using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.Net.Gateway.Association.Models;
using Pomelo.Net.Gateway.EndpointManager;
using Pomelo.Net.Gateway.Router;
using Pomelo.Net.Gateway.Server.Models;
using Pomelo.Net.Gateway.Tunnel;

namespace Pomelo.Net.Gateway.Server.Controllers
{
    public class PublicRuleController : Controller
    {
        private readonly ILogger<PublicRuleController> _logger;

        public PublicRuleController(ILogger<PublicRuleController> logger)
        {
            _logger = logger;
        }

        public async ValueTask<IActionResult> Index(
            [FromServices] IServiceProvider services,
            [FromServices] ServerContext db,
            CancellationToken cancellationToken = default)
        {
            ViewBag.StreamTunnels = services.GetServices<IStreamTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.StreamRouters = services.GetServices<IStreamRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.PacketTunnels = services.GetServices<IPacketTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);
            ViewBag.PacketRouters = services.GetServices<IPacketRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToDictionary(x => x.Id);

            return View(await db.PublicRules.ToListAsync(cancellationToken));
        }

        [HttpGet]
        public IActionResult Create([FromServices] IServiceProvider services)
        {
            ViewBag.StreamTunnels = services.GetServices<IStreamTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.StreamRouters = services.GetServices<IStreamRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.PacketTunnels = services.GetServices<IPacketTunnel>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
            ViewBag.PacketRouters = services.GetServices<IPacketRouter>()
                .Select(x => new Interface
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();

            return View();
        }

        [HttpPost]
        public async ValueTask<IActionResult> Create(
            [FromServices] ServerContext db,
            [FromServices] TcpEndpointManager tcpEndpointManager,
            PublicRule model,
            CancellationToken cancellationToken = default)
        {
            if (await db.Users.AnyAsync(x => x.Username == model.Id))
            {
                return Content($"The ID {model.Id} is conflicted");
            }

            IPEndPoint serverEndpoint, destinationEndpoint;
            try
            {
                serverEndpoint = IPEndPoint.Parse(model.ServerEndpoint);
                destinationEndpoint = await AddressHelper.ParseAddressAsync(model.DestinationEndpoint, 0);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.ToString());
                return Content("Endpoint is invalid");
            }

            db.PublicRules.Add(model);
            await db.SaveChangesAsync();
            tcpEndpointManager.InsertPreCreateEndpointRuleAsync(
                model.Id,
                model.Protocol,
                serverEndpoint,
                destinationEndpoint,
                model.RouterId,
                model.TunnelId,
                cancellationToken);
            tcpEndpointManager.GetOrCreateListenerForEndpoint(
                serverEndpoint,
                model.RouterId,
                model.TunnelId,
                model.Id,
                EndpointCollection.EndpointUserType.Public);
            return RedirectToAction(nameof(Index));
        }
    }
}