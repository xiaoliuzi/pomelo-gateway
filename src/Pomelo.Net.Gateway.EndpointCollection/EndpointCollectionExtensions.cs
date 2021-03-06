using Microsoft.EntityFrameworkCore;
using Pomelo.Net.Gateway.EndpointCollection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class EndpointCollectionExtensions
    {
        public static IServiceCollection AddPomeloGatewayEndpointCollection(this IServiceCollection services)
        {
            return services.AddDbContext<EndpointContext>(x => x.UseInMemoryDatabase("pomelo-gateway"));
        }
    }
}
