namespace FraudEvaluation.Application.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public ErrorCode? Code { get; }

        protected Result(bool isSuccess, string? error, ErrorCode? code)
        {
            IsSuccess = isSuccess;
            Error = error;
            Code = code;
        }

        public static Result<T> Ok<T>(T value) => new Result<T>(true, null, null, value);
        public static Result<T> Fail<T>(string error, ErrorCode? errorCode = null) => new(false, error, errorCode, default);
        public static Result<T> NotFound<T>(string? message = null) => Fail<T>(message ?? "NotFound", FraudEvaluation.Application.Common.ErrorCode.NotFound);
    }

    public class Result<T> : Result
    {
        public T? Value { get; }

        internal Result(bool isSuccess, string? error, ErrorCode? errorCode, T? value)
            : base(isSuccess, error, errorCode)
        {
            Value = value;
        }
    }
}
