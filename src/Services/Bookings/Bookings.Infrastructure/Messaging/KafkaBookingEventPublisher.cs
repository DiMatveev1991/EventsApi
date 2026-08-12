using System.Text.Json;
using Bookings.Application.Abstractions;
using Confluent.Kafka;
using Contracts;

namespace Bookings.Infrastructure.Messaging;

public sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingEventPublisher(string bootstrapServers)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public async Task PublishAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        await _producer.ProduceAsync(
            KafkaTopics.BookingConfirmed,
            new Message<string, string>
            {
                Key = message.EventId.ToString("N"),
                Value = JsonSerializer.Serialize(message)
            },
            cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}
