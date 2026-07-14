using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Http;
using FraudEvaluation.Application.Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
// Register MediatR handlers from the Application project
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FraudEvaluation.Application.MediatRMarker).Assembly));

// Register infrastructure services (Redis, Postgres, RabbitMQ) using configuration
var configuration = builder.Configuration;
var redisConn = configuration.GetValue<string>("Redis:Connection") ?? "localhost:6379";
var pgConn = configuration.GetValue<string>("ConnectionStrings:Postgres") ?? "Host=localhost;Database=fraud;Username=postgres;Password=postgres";
var rabbitConn = configuration.GetValue<string>("RabbitMQ:Connection") ?? "amqp://guest:guest@localhost:5672/";

builder.Services.AddSingleton<FraudEvaluation.Application.Interfaces.ICacheService>(sp => new FraudEvaluation.Infrastructure.Cache.RedisCacheService(redisConn));
builder.Services.AddScoped<FraudEvaluation.Domain.Repositories.ITransactionRepository>(sp => new FraudEvaluation.Infrastructure.Repositories.PostgresTransactionRepository(pgConn));
builder.Services.AddSingleton<FraudEvaluation.Application.Interfaces.IMessagePublisher>(sp => new FraudEvaluation.Infrastructure.Messaging.RabbitMqPublisher(rabbitConn));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Expose OpenAPI and Swagger UI in Development
    app.MapOpenApi();

    // Scalar.AspNetCore integration (development only, no authentication)
    try
    {
        var scalarAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Scalar.AspNetCore", StringComparison.OrdinalIgnoreCase));
        if (scalarAssembly == null)
        {
            scalarAssembly = Assembly.Load("Scalar.AspNetCore");
        }

        if (scalarAssembly != null)
        {
            var extensionMethods = scalarAssembly.GetTypes()
                .Where(t => t.IsSealed && t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.Name.Contains("UseScalar", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("MapScalar", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("UseScalarUI", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("MapScalarUI", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var method in extensionMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(app.GetType()))
                {
                    method.Invoke(null, new object[] { app });
                    break;
                }

                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(Microsoft.AspNetCore.Builder.IApplicationBuilder)))
                {
                    method.Invoke(null, new object[] { app });
                    break;
                }
            }
        }
    }
    catch
    {
        // Scalar integration is optional in development; ignore failures
    }
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// POST endpoint: submit fraud evaluation request
app.MapPost("/fraud-evaluations", async (TransactionRequest req, HttpRequest httpReq, IMediator mediator) =>
{
    // Check Idempotency-Key header
    if (!httpReq.Headers.TryGetValue("Idempotency-Key", out var idempotencyValues) || string.IsNullOrWhiteSpace(idempotencyValues))
    {
        return Results.BadRequest(new { error = "Missing Idempotency-Key header." });
    }

    var idempotencyKey = idempotencyValues.ToString();
    if (!Guid.TryParse(idempotencyKey, out _))
    {
        return Results.BadRequest(new { error = "Idempotency-Key must be a valid GUID." });
    }

    if (string.IsNullOrWhiteSpace(req.TaxId) || !req.TaxId.All(char.IsDigit))
    {
        return Results.BadRequest(new { error = "TaxId must contain only digits." });
    }

    if (req.Amount <= 0)
    {
        return Results.BadRequest(new { error = "Amount must be greater than zero." });
    }

    if (string.IsNullOrWhiteSpace(req.Currency) || req.Currency.Length != 2 || !(req.Currency == "R$" || req.Currency == "U$" || req.Currency == "E$"))
    {
        return Results.BadRequest(new { error = "Currency must be one of: R$, U$, E$." });
    }

    var ip = httpReq.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

    var command = new FraudEvaluation.Application.Commands.SubmitFraudEvaluationCommand(idempotencyKey, ip, req.TaxId, req.Amount, req.Currency);
    var result = await mediator.Send(command);

    if (result.AlreadyExists)
    {
        return Results.Ok(new { transactionId = result.TransactionId });
    }

    return Results.Accepted($"/transactions/{result.TransactionId}", new { transactionId = result.TransactionId });
})
.WithName("SubmitFraudEvaluation");

// GET endpoint: consulta status da transação
app.MapGet("/transactions/{id}", async (string id, IMediator mediator) =>
{
    var result = await mediator.Send(new FraudEvaluation.Application.Queries.GetTransactionStatusQuery(id));

    if (!result.IsSuccess)
    {
        return result.Code switch
        {
            ErrorCode.InvalidId => Results.BadRequest(new { error = result.Error ?? "Invalid id" }),
            ErrorCode.NotFound => Results.NotFound(),
            ErrorCode.ValidationFailed => Results.UnprocessableEntity(new { error = result.Error }),
            _ => Results.BadRequest(new { error = result.Error ?? "An error occurred" }),
        };
    }

    var value = result.Value!;
    return Results.Ok(new { transactionId = value.TransactionId, validationStatus = value.ValidationStatus, transactionStatus = value.TransactionStatus });
})
.WithName("GetTransactionStatus");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record TransactionRequest(string TaxId, decimal Amount, string Currency);
