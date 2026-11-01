namespace CryptoStuff.Api;

/// <summary>OpenAPI metadata shared by the route groups.</summary>
internal static class EndpointMetadataExtensions
{
    /// <summary>
    /// Documents a route that returns <typeparamref name="TView"/> on success and
    /// the validation and service-failure problems the data routes can produce.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata<TView>(this RouteHandlerBuilder builder) =>
        builder
            .Produces<TView>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Documents a route that returns <typeparamref name="TView"/> with
    /// <paramref name="successStatusCode"/> on success and, besides <c>500</c>,
    /// exactly the problems in <paramref name="problemStatusCodes"/>.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata<TView>(
        this RouteHandlerBuilder builder, int successStatusCode, params int[] problemStatusCodes) =>
        builder.Produces<TView>(successStatusCode).WithProblems(problemStatusCodes);

    /// <summary>
    /// Documents a route that returns no body with <paramref name="successStatusCode"/> on success and,
    /// besides <c>500</c>, exactly the problems in <paramref name="problemStatusCodes"/>.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata(
        this RouteHandlerBuilder builder, int successStatusCode, params int[] problemStatusCodes) =>
        builder.Produces(successStatusCode).WithProblems(problemStatusCodes);

    private static RouteHandlerBuilder WithProblems(this RouteHandlerBuilder builder, int[] problemStatusCodes)
    {
        foreach (var statusCode in problemStatusCodes)
        {
            if (statusCode == StatusCodes.Status400BadRequest)
            {
                builder.ProducesValidationProblem();
            }
            else
            {
                builder.ProducesProblem(statusCode);
            }
        }

        return builder.ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
