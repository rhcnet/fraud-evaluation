using FraudEvaluation.API.Extensions;
using FraudEvaluation.Application.Queries;
using MediatR;

namespace FraudEvaluation.API.Endpoints
{
    public static class ApiEndpoints
    {
        public static void MapTransactionEndpoints(this WebApplication app)
        {
            app.MapGet("/transactions/{id}", async (string id, IMediator mediator) =>
            {
                var result = await mediator.Send(new GetTransactionStatusQuery(id));

                return result.ToIResult(value => Results.Ok(new
                {
                    transactionId = value.TransactionId,
                    validationStatus = value.ValidationStatus?.ToString()?.ToUpperInvariant(),
                    transactionStatus = value.TransactionStatus.ToString().ToUpperInvariant()
                }));
            })
            .WithName("GetTransactionStatus");
        }
    }
}
