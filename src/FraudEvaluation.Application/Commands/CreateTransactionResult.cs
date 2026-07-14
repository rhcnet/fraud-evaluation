namespace FraudEvaluation.Application.Commands
{
    public record CreateTransactionResult(System.Guid Id, string TaxId, decimal Amount);
}
