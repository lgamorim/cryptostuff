namespace CryptoStuff.Api;

/// <summary>OpenAPI metadata shared by the route groups.</summary>
internal static class EndpointMetadataExtensions
{
    /// <summary>
    /// Documents a route that returns <typeparamref name="TView"/> on success and
    /// the validation and service-failure problems the data routes can produce,
    /// plus the <c>401</c> every protected route answers without a valid API key.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata<TView>(this RouteHandlerBuilder builder) =>
        builder
            .Produces<TView>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Documents a route that returns <typeparamref name="TView"/> with
    /// <paramref name="successStatusCode"/> on success and, besides the API
    /// key's <c>401</c> and <c>500</c>, exactly the problems in
    /// <paramref name="problemStatusCodes"/>. A route that saves adds the
    /// database-busy <c>503</c> with <see cref="WithDatabaseBusyProblem"/>.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata<TView>(
        this RouteHandlerBuilder builder, int successStatusCode, params int[] problemStatusCodes) =>
        builder.Produces<TView>(successStatusCode).WithProblems(problemStatusCodes);

    /// <summary>
    /// Documents a route that returns no body with <paramref name="successStatusCode"/> on success and,
    /// besides the API key's <c>401</c> and <c>500</c>, exactly the problems in <paramref name="problemStatusCodes"/>.
    /// A route that saves adds the database-busy <c>503</c> with <see cref="WithDatabaseBusyProblem"/>.
    /// </summary>
    public static RouteHandlerBuilder WithProblemMetadata(
        this RouteHandlerBuilder builder, int successStatusCode, params int[] problemStatusCodes) =>
        builder.Produces(successStatusCode).WithProblems(problemStatusCodes);

    /// <summary>
    /// Documents the <c>503</c> that <see cref="PersistenceBusyResponses"/>
    /// answers when a write finds the database locked past SQLite's wait. Only
    /// a route that saves can get it, so only those routes add it.
    /// </summary>
    public static RouteHandlerBuilder WithDatabaseBusyProblem(this RouteHandlerBuilder builder) =>
        builder.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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

        return builder
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
