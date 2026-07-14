namespace FraudEvaluation.Domain.Entities
{
    record Transaction(string TaxId, decimal Amount, string Currency);
}
