using FraudEvaluation.API.Extensions;
using FraudEvaluation.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

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

            // POST endpoint: submit fraud evaluation request
            app.MapPost("/fraud-evaluations", async (TransactionRequest req, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, HttpRequest httpReq, IMediator mediator) =>
            {
                // Check Idempotency-Key header
                if (string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    return Results.BadRequest(new { error = "Missing Idempotency-Key header." });
                }

                // Business parameter validation moved to handler. Keep Idempotency-Key and IP checks at endpoint as requested.

                var ip = httpReq.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

                var command = new Application.Commands.SubmitFraudEvaluationCommand(idempotencyKey, ip, req.TaxId, req.Amount, req.Currency);
                var result = await mediator.Send(command);

                return result.ToIResult(value =>
                {
                    if (value.AlreadyExists)
                    {
                        return Results.Ok(new { transactionId = value.TransactionId });
                    }

                    return Results.Accepted($"/transactions/{value.TransactionId}", new { transactionId = value.TransactionId });
                });
            })
            .WithName("SubmitFraudEvaluation");
        }
    }

    record TransactionRequest(string TaxId, decimal Amount, string Currency);
}
