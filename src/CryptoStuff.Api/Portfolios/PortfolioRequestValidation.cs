using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Api.Portfolios;

/// <summary>
/// Turns the portfolio routes' raw body values into typed ones through Core's
/// validators and token parsers, so the API accepts exactly what the CLI does,
/// reporting the first problem as the <c>400</c> validation-problem result.
/// </summary>
internal static class PortfolioRequestValidation
{
    public static (CostBasisMethod Method, IResult? Problem) ParseCostBasisMethod(string? raw, string fieldName)
    {
        if (CostBasisMethodParser.TryParse(raw, out var method))
        {
            return (method, null);
        }

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return (default, RequestValidation.ValidationProblem(fieldName, CostBasisMethodParser.Validate(raw, fieldName).ErrorMessage!));
    }

    public static (TransactionDetails? Details, IResult? Problem) ParseTransaction(TransactionRequest? request)
    {
        if (!TransactionKindParser.TryParse(request?.Kind, out var kind))
        {
            // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
            return (null, RequestValidation.ValidationProblem("kind", TransactionKindParser.Validate(request?.Kind, "kind").ErrorMessage!));
        }

        var coinProblem = RequestValidation.ValidateScalar(request?.CoinId, "coinId");
        if (coinProblem is not null)
        {
            return (null, coinProblem);
        }

        var quantityProblem = RequestValidation.ValidateAmount(request?.Quantity, "quantity", PositiveDecimalValidator.Validate);
        if (quantityProblem is not null)
        {
            return (null, quantityProblem);
        }

        var unitPriceProblem = RequestValidation.ValidateAmount(request?.UnitPrice, "unitPrice", NonNegativeDecimalValidator.Validate);
        if (unitPriceProblem is not null)
        {
            return (null, unitPriceProblem);
        }

        if (!TimestampFormatValidator.TryParse(request?.OccurredAt, out var occurredAt))
        {
            // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
            var message = TimestampFormatValidator.Validate(request?.OccurredAt, "occurredAt").ErrorMessage!;
            return (null, RequestValidation.ValidationProblem("occurredAt", message));
        }

        var fee = request?.Fee ?? 0m;
        var feeProblem = RequestValidation.ValidateAmount(fee, "fee", NonNegativeDecimalValidator.Validate);
        if (feeProblem is not null)
        {
            return (null, feeProblem);
        }

        // The checks above returning null guarantee request, its CoinId, Quantity and UnitPrice are non-null.
        var details = new TransactionDetails
        {
            Kind = kind,
            CoinId = request!.CoinId!,
            Quantity = request.Quantity!.Value,
            UnitPrice = request.UnitPrice!.Value,
            Fee = fee,
            OccurredAt = occurredAt,
        };
        return (details, null);
    }
}
