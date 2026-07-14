using MediatR;
using Microsoft.Extensions.Logging;

namespace FraudEvaluation.Application.Handlers;

// Handler for ProcessTransactionCommand. Actual processing will be implemented later.
public class ProcessTransactionHandler(ILogger<ProcessTransactionHandler> logger) : IRequestHandler<Commands.ProcessTransactionCommand, Common.Result<string>>
{
    private readonly ILogger<ProcessTransactionHandler> _logger = logger;

    public async Task<Common.Result<string>> Handle(Commands.ProcessTransactionCommand request, CancellationToken cancellationToken)
    {
        // Placeholder for processing logic (to be implemented later)
        _logger.LogInformation("Received ProcessTransactionCommand: {tx} TaxId={tax} Amount={amount} Currency={currency}", request.TransacationId, request.TaxId, request.Amount, request.Currency.Code);

        await Task.CompletedTask;
        return Common.Result.Ok("OK");
    }
}
