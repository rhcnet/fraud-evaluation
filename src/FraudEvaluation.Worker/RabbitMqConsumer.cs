using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FraudEvaluation.Worker;

public class RabbitMqConsumer : BackgroundService
{
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqConsumer(ILogger<RabbitMqConsumer> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var connStr = _configuration.GetValue<string>("RabbitMQ:Connection") ?? "amqp://guest:guest@localhost:5672/";
        var factory = new ConnectionFactory() { Uri = new Uri(connStr) };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange and queue, bind
        _channel.ExchangeDeclare("fraud.evaluations", ExchangeType.Fanout, durable: true);
        _channel.QueueDeclare(queue: "fraud.evaluations.queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueBind("fraud.evaluations.queue", "fraud.evaluations", string.Empty);

        // Basic QoS
        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("RabbitMQ consumer connected and listening on queue fraud.evaluations.queue");

        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null)
        {
            _logger.LogError("RabbitMQ channel is not initialized");
            return Task.CompletedTask;
        }

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received message: {msg}", message);

                // Try parse JSON
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;
                    var txId = root.GetProperty("transactionId").GetGuid();
                    var taxId = root.GetProperty("taxId").GetString();
                    var amount = root.GetProperty("amount").GetDecimal();
                    var currency = root.GetProperty("currency").GetString();
                    // Use domain value object for currency
                    string currencyCode = currency ?? "BRL";
                    try
                    {
                        var currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create(currencyCode);
                        currencyCode = currencyVo.Code;
                    }
                    catch
                    {
                        // fallback to BRL if invalid
                        currencyCode = "BRL";
                    }

                    _logger.LogInformation("Processing transaction {tx} - TaxId {tax} Amount {amount} {currency}", txId, taxId, amount, currencyCode);
                    // TODO: implement processing logic (call DB, call services, etc.)
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse message JSON");
                }

                // Acknowledge
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message");
            }
        };

        _channel.BasicConsume(queue: "fraud.evaluations.queue", autoAck: false, consumer: consumer);

        // Keep running until cancellation
        return Task.Run(() => { while (!stoppingToken.IsCancellationRequested) Task.Delay(1000, stoppingToken).Wait(stoppingToken); }, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection");
        }

        return base.StopAsync(cancellationToken);
    }
}
