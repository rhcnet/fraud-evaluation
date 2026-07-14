using MediatR;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;

namespace FraudEvaluation.Application.Handlers;

// Handler for ProcessTransactionCommand. Actual processing will be implemented later.
public class ProcessTransactionHandler(ILogger<ProcessTransactionHandler> logger) : IRequestHandler<Commands.ProcessTransactionCommand, Common.Result<string>>
{
    private readonly ILogger<ProcessTransactionHandler> _logger = logger;

    private class TaxCheckResponse
    {
        public bool Valid { get; set; }
    }

    public async Task<Common.Result<string>> Handle(Commands.ProcessTransactionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received ProcessTransactionCommand: {tx} TaxId={tax} Amount={amount} Currency={currency}", request.TransacationId, request.TaxId, request.Amount, request.Currency.Code);

        try
        {
            var url = $"https://localhost:7253/check-tax-id/{request.TaxId}";
            using var httpClient = new HttpClient();
            var result = await httpClient.GetFromJsonAsync<TaxCheckResponse>(url, cancellationToken);

            if (result is not null && result.Valid)
            {
                return Common.Result.Ok("OK");
            }

            return Common.Result.Fail<string>("TaxId invalid");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking TaxId {taxId}", request.TaxId);
            return Common.Result.Fail<string>("Error checking TaxId");
        }
    }
}
