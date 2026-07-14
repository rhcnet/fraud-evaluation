using FraudEvaluation.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();
host.Run();
