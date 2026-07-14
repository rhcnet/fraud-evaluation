namespace FraudEvaluation.Domain.ValueObjects
{
    public record Currency
    {
        public string Code { get; }

        // Lista de moedas permitidas (pode vir de uma config ou enum)
        private static readonly HashSet<string> AllowedCodes = ["BRL", "USD", "EUR"];

        private Currency(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !AllowedCodes.Contains(code.ToUpper()))
            {
                // Garante a resiliência impedindo a criação de um estado inválido
                throw new DomainException($"Invalid currency code: {code}. Allowed: {string.Join(", ", AllowedCodes)}");
            }

            Code = code.ToUpper();
        }

        public static Currency Create(string code) => new(code);

        // Conversão implícita para facilitar o uso como string
        public static implicit operator string(Currency currency) => currency.Code;
    }
}
