using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Http;
using FraudEvaluation.Application.Common;
using FraudEvaluation.API.Endpoints;

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

// POST endpoint: submit fraud evaluation request
app.MapPost("/fraud-evaluations", async (TransactionRequest req, HttpRequest httpReq, IMediator mediator) =>
{
    // Check Idempotency-Key header
    if (!httpReq.Headers.TryGetValue("Idempotency-Key", out var idempotencyValues) || string.IsNullOrWhiteSpace(idempotencyValues))
    {
        return Results.BadRequest(new { error = "Missing Idempotency-Key header." });
    }

    var idempotencyKey = idempotencyValues.ToString();

    // Business parameter validation moved to handler. Keep Idempotency-Key and IP checks at endpoint as requested.

    var ip = httpReq.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

    var command = new FraudEvaluation.Application.Commands.SubmitFraudEvaluationCommand(idempotencyKey, ip, req.TaxId, req.Amount, req.Currency);
    try
    {
        var result = await mediator.Send(command);

        if (result.AlreadyExists)
        {
            return Results.Ok(new { transactionId = result.TransactionId });
        }

        return Results.Accepted($"/transactions/{result.TransactionId}", new { transactionId = result.TransactionId });
    }
    catch (FraudEvaluation.Application.Common.ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("SubmitFraudEvaluation");

// Map transaction-related endpoints
app.MapTransactionEndpoints();

app.Run();

record TransactionRequest(string TaxId, decimal Amount, string Currency);
