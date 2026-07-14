using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using FraudEvaluation.Application.Interfaces;

namespace FraudEvaluation.Infrastructure.Messaging
{
    public class RabbitMqPublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMqPublisher(string connectionString)
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(connectionString)
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare("fraud.evaluations", ExchangeType.Fanout, durable: true);
        }

        public Task PublishAsync(string routingKey, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "fraud.evaluations", routingKey: routingKey, basicProperties: null, body: body);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
            }
            catch {}
        }
    }
}
