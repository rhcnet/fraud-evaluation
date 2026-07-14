using System.Threading.Tasks;

namespace FraudEvaluation.Application.Interfaces
{
    public interface IMessagePublisher
    {
        Task PublishAsync(string routingKey, string message);
    }
}
