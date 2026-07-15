using FraudEvaluation.API.Endpoints;
using FraudEvaluation.API.Extensions;
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
app.UseDevelopmentConfiguration();

app.UseHttpsRedirection();

// Map transaction-related endpoints
app.MapTransactionEndpoints();

app.Run();
