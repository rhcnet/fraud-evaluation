namespace FraudEvaluation.Domain.Entities
{
    public class TransactionEntity
    {
        public Guid Id { get; set; }
        public string IdempotencyKey { get; set; } = null!;
        public string Ip { get; set; } = null!;
        public string TaxId { get; set; } = null!;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ValidationStatus? ValidationStatus { get; set; }
        public TransactionStatus TransactionStatus { get; set; } = Domain.Entities.TransactionStatus.Processing;
    }
}
