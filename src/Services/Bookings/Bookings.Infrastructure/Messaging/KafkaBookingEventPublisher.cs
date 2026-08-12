using System.Text.Json;
using Bookings.Application.Abstractions;
using Confluent.Kafka;
using Contracts;

namespace Bookings.Infrastructure.Messaging;

/// <summary>Публикует подтверждения бронирований в Kafka.</summary>
public sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    /// <summary>Создаёт идемпотентный producer с обязательным подтверждением брокера.</summary>
    public KafkaBookingEventPublisher(string bootstrapServers)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    /// <summary>Сериализует и публикует подтверждение бронирования.</summary>
    public async Task PublishAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        await _producer.ProduceAsync(
            KafkaTopics.BookingConfirmed,
            new Message<string, string>
            {
                // EventId обеспечивает один partition и порядок всех броней события.
                Key = message.EventId.ToString("N"),
                Value = JsonSerializer.Serialize(message)
            },
            cancellationToken);
    }

    /// <summary>Дожидается отправки буфера и освобождает Kafka producer.</summary>
    public void Dispose()
    {
        try
        {
            // Явный Flush уменьшает риск потери последних сообщений при штатной остановке.
            _producer.Flush(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}
