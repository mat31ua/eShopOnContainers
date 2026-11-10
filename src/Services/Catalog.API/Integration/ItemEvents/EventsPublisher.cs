using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Catalog.API.Integration.ItemEvents
{
    public interface IEventsPublisher
    {
        Task Start();
        Task Stop();
        Task<bool> Publish(object message);
    }

    public class EventsPublisher : IEventsPublisher
    {
        private readonly RabbitOptions _rabbitOptions;
        private readonly ConnectionFactory _factory;
        private readonly ILogger<EventsPublisher> _logger;

        private readonly string _exchange;
        private readonly string _queueName;
        private readonly string _routingKey;

        private IConnection? _connection;
        private IChannel? _channel;

        private readonly SemaphoreSlim _publishLock = new(1, 1);

        public EventsPublisher(
            IOptions<RabbitOptions> rabbitOptions,
            IOptions<RabbitMqExchange> exchangeOptions,
            ILogger<EventsPublisher> logger)
        {
            _rabbitOptions = rabbitOptions.Value;
            _logger = logger;

            _exchange = exchangeOptions.Value.Exchange;
            _queueName = exchangeOptions.Value.QueueName;
            _routingKey = exchangeOptions.Value.RoutingKeys;

            _factory = new ConnectionFactory
            {
                HostName = _rabbitOptions.HostName,
                Port = _rabbitOptions.Port,
                UserName = _rabbitOptions.UserName,
                Password = _rabbitOptions.Password,
                VirtualHost = "/",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(15)
            };
        }

        public bool IsConnected =>
            _connection?.IsOpen == true &&
            _channel?.IsOpen == true;

        private async Task<bool> TryConnect()
        {
            //int maxAttempts = Math.Max(1, _rabbitOptions.RetryAttempt);
            int maxAttempts = 10;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation(
                        "Intentando conectar con RabbitMQ en {HostName}. " +
                        "Intento {Attempt} de {MaxAttempts}",
                        _rabbitOptions.HostName,
                        attempt,
                        maxAttempts);

                    _connection = await _factory.CreateConnectionAsync();

                    _logger.LogInformation(
                        "Conexión establecida con RabbitMQ en {HostName}",
                        _connection.Endpoint.HostName);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error al conectar con RabbitMQ. " +
                        "Intento {Attempt} de {MaxAttempts}",
                        attempt,
                        maxAttempts);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
            }

            _logger.LogError(
                "No se ha podido establecer la conexión con RabbitMQ.");

            return false;
        }

        public async Task Start()
        {
            if (IsConnected)
            {
                return;
            }

            if (!await TryConnect())
            {
                throw new InvalidOperationException(
                    "No se ha podido iniciar el publicador de RabbitMQ.");
            }

            try
            {
                _channel = await _connection!.CreateChannelAsync();

                await _channel.ExchangeDeclareAsync(
                    exchange: _exchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                await _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                await _channel.QueueBindAsync(
                    queue: _queueName,
                    exchange: _exchange,
                    routingKey: _routingKey);

                _logger.LogInformation(
                    "Publicador iniciado. Exchange: {Exchange}, " +
                    "cola: {Queue}, routing key: {RoutingKey}",
                    _exchange,
                    _queueName,
                    _routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al preparar el canal de publicación.");

                await Stop();
                throw;
            }
        }

        public async Task<bool> Publish(object message)
        {
            ArgumentNullException.ThrowIfNull(message);

            await _publishLock.WaitAsync();

            try
            {
                if (!IsConnected)
                {
                    _logger.LogWarning(
                        "No se puede publicar el evento: " +
                        "la conexión o el canal no están disponibles.");

                    return false;
                }

                string json = JsonSerializer.Serialize(
                    message,
                    message.GetType());

                byte[] body = Encoding.UTF8.GetBytes(json);

                var properties = new BasicProperties
                {
                    ContentType = "application/json",
                    Persistent = true
                };

                await _channel!.BasicPublishAsync(
                    exchange: _exchange,
                    routingKey: _routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Llamada de publicación completada. " +
                    "Tipo de evento: {EventType}, exchange: {Exchange}, " +
                    "routing key: {RoutingKey}",
                    message.GetType().Name,
                    _exchange,
                    _routingKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al publicar el evento {EventType}.",
                    message.GetType().Name);

                return false;
            }
            finally
            {
                _publishLock.Release();
            }
        }

        public async Task Stop()
        {
            await _publishLock.WaitAsync();

            try
            {
                if (_channel is not null)
                {
                    try
                    {
                        if (_channel.IsOpen)
                        {
                            await _channel.CloseAsync();
                        }
                    }
                    finally
                    {
                        await _channel.DisposeAsync();
                        _channel = null;
                    }
                }
            }
            finally
            {
                try
                {
                    if (_connection is not null)
                    {
                        try
                        {
                            if (_connection.IsOpen)
                            {
                                await _connection.CloseAsync();
                            }
                        }
                        finally
                        {
                            await _connection.DisposeAsync();
                            _connection = null;
                        }
                    }
                }
                finally
                {
                    _publishLock.Release();
                }
            }
        }
    }
}
