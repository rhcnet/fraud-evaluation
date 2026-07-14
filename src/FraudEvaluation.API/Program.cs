using FraudEvaluation.API.Endpoints;
using System.Reflection;

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
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Scalar.AspNetCore", StringComparison.OrdinalIgnoreCase)) ?? Assembly.Load("Scalar.AspNetCore");
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

// Map transaction-related endpoints
app.MapTransactionEndpoints();

app.Run();
