namespace Catalog.API.Integration
{
    public class RabbitMqExchange
    {
        public string Exchange { get; set; }
        public string QueueName { get; set; }
        public string RoutingKeys { get; set; }
    }
}
