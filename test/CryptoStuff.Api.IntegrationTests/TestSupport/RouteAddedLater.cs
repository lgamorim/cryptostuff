using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Maps one more route, <c>GET <see cref="Path"/></c> answering
/// <see cref="Body"/>, on the host's own endpoint route builder once
/// <c>Program</c> has configured its pipeline: the route a later author would
/// add beside the others, declaring nothing about authorization.
/// </summary>
internal sealed class RouteAddedLater : IStartupFilter
{
    public const string Path = "/added-later";

    public const string Body = "added later";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            next(app);

            // The WebApplication itself is the endpoint route builder that
            // Program's Map* calls use; WebApplication shares it with the
            // pipeline it builds through the builder's properties.
            var endpoints = app.Properties.Values.OfType<IEndpointRouteBuilder>().First();
            endpoints.MapGet(Path, () => Body);
        };
}
