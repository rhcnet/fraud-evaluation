using System;
using System.Collections.Generic;

namespace FraudEvaluation.Domain.ValueObjects
{
    public record Currency
    {
        public string Code { get; }

        // Lista de moedas permitidas (pode vir de uma config ou enum)
        private static readonly HashSet<string> AllowedCodes = ["BRL", "USD", "EUR"];

        private Currency(string code)
        {
            // assume caller validated
            Code = code.ToUpperInvariant();
        }

        public static Currency Create(string code)
        {
            if (TryCreate(code, out var currency))
            {
                return currency!;
            }

            throw new DomainException($"Invalid currency code: {code}. Allowed: {string.Join(", ", AllowedCodes)}");
        }

        // TryCreate avoids using exceptions for control flow when validating input
        public static bool TryCreate(string? code, out Currency? currency)
        {
            currency = null;
            if (string.IsNullOrWhiteSpace(code)) return false;

            var up = code.ToUpperInvariant();
            if (!AllowedCodes.Contains(up)) return false;

            currency = new Currency(up);
            return true;
        }

        // Conversão implícita para facilitar o uso como string
        public static implicit operator string(Currency currency) => currency.Code;
    }
}
