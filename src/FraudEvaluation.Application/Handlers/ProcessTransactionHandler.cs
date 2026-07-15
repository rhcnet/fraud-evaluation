using MediatR;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using FraudEvaluation.Domain.Repositories;
using FraudEvaluation.Domain.Entities;

namespace FraudEvaluation.Application.Handlers;

// Handler for ProcessTransactionCommand. Actual processing will be implemented later.
public class ProcessTransactionHandler(ILogger<ProcessTransactionHandler> logger, ITransactionRepository repo) : IRequestHandler<Commands.ProcessTransactionCommand, Common.Result<string>>
{
    private readonly ILogger<ProcessTransactionHandler> _logger = logger;
    private readonly ITransactionRepository _repo = repo;

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

            // Determine validation status based on tax-id check (and other validations if added)
            var validationStatus = (result is not null && result.Valid) ? ValidationStatus.Approved : ValidationStatus.Rejected;

            // Load transaction and update statuses
            var transaction = await _repo.GetByIdAsync(request.TransacationId);
            if (transaction == null)
            {
                _logger.LogWarning("Transaction {tx} not found to update statuses", request.TransacationId);
                return Common.Result.NotFound<string>("Transaction not found");
            }

            transaction.ValidationStatus = validationStatus;
            transaction.TransactionStatus = TransactionStatus.Processed;
            transaction.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(transaction);

            if (validationStatus == ValidationStatus.Approved)
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
