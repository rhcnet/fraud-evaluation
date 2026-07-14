using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MediatR;

namespace FraudEvaluation.Worker;

public class RabbitMqConsumer(ILogger<RabbitMqConsumer> logger, IConfiguration configuration, IMediator mediator) : BackgroundService
{
    private readonly ILogger<RabbitMqConsumer> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly IMediator _mediator = mediator;
    private IConnection? _connection;
    private IModel? _channel;

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
        consumer.Received += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received message: {msg}", message);

                // Parse JSON into a typed object
                var parsed = ParseMessage(message);
                if (parsed == null)
                {
                    _logger.LogWarning("Failed to parse message JSON");
                }
                else
                {
                    _logger.LogInformation("Processing transaction {tx} - TaxId {tax} Amount {amount} {currency}", parsed.TransactionId, parsed.TaxId, parsed.Amount, parsed.CurrencyCode);

                    // Send command to application layer for processing
                    try
                    {
                        var result = await _mediator.Send(new FraudEvaluation.Application.Commands.ProcessTransactionCommand(parsed.TransactionId, parsed.TaxId, parsed.Amount, parsed.Currency));
                        if (result.IsSuccess)
                        {
                            _logger.LogInformation("ProcessTransactionCommand succeeded: {msg}", result.Value);
                        }
                        else
                        {
                            _logger.LogWarning("ProcessTransactionCommand failed: {error}", result.Error);
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending ProcessTransactionCommand");
                    }

                    // Acknowledge
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
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

    // Parses incoming JSON message and returns a typed object on success, otherwise null
    private ParsedTransaction? ParseMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            var txId = root.GetProperty("transactionId").GetGuid();
            var taxId = root.GetProperty("TaxId").GetString();
            var amount = root.GetProperty("Amount").GetDecimal();
            var currency = root.GetProperty("currency").GetString();

            // Use domain value object for currency
            string currencyCode = currency ?? "BRL";
            FraudEvaluation.Domain.ValueObjects.Currency currencyVo;
            try
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create(currencyCode);
            }
            catch
            {
                currencyVo = FraudEvaluation.Domain.ValueObjects.Currency.Create("BRL");
            }

            return new ParsedTransaction(txId, taxId ?? string.Empty, amount, currencyVo);
        }
        catch
        {
            return null;
        }
    }

    private sealed class ParsedTransaction
    {
        public Guid TransactionId { get; }
        public string TaxId { get; }
        public decimal Amount { get; }
        public FraudEvaluation.Domain.ValueObjects.Currency Currency { get; }
        public string CurrencyCode => Currency.Code;

        public ParsedTransaction(Guid transactionId, string taxId, decimal amount, FraudEvaluation.Domain.ValueObjects.Currency currency)
        {
            TransactionId = transactionId;
            TaxId = taxId;
            Amount = amount;
            Currency = currency;
        }
    }
}
