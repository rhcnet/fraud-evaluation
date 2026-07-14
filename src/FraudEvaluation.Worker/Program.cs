using FraudEvaluation.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<RabbitMqConsumer>();

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



var host = builder.Build();
host.Run();
