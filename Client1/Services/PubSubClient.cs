using Client1;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcPubSub;

namespace Client1.Services
{
	public class PubSubClient
	{
		private readonly PubSubService.PubSubServiceClient _client;
		private readonly ILogger<PubSubClient> _logger;

		public PubSubClient(string serverAddress, ILogger<PubSubClient> logger)
		{
			var channel = GrpcChannel.ForAddress(serverAddress);
			_client = new PubSubService.PubSubServiceClient(channel);
			_logger = logger;
		}

		public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
		{
			var request = new SubscribeRequest { Topic = topic };

			using var call = _client.Subscribe(request, cancellationToken: cancellationToken);

			_logger.LogInformation($"Subscribed to topic: {topic}");

			try
			{
				await foreach (var message in call.ResponseStream.ReadAllAsync(cancellationToken))
				{
					_logger.LogInformation($"Received message on {message.Topic}: {message.Content} (at {DateTimeOffset.FromUnixTimeSeconds(message.Timestamp)})");

					// Handle the message here
					await HandleMessage(message);
				}
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
			{
				_logger.LogInformation($"Subscription to {topic} was cancelled");
			}
		}

		public async Task<bool> PublishAsync(string topic, string content)
		{
			var request = new PublishRequest
			{
				Topic = topic,
				Content = content
			};

			try
			{
				var response = await _client.PublishAsync(request);
				_logger.LogInformation($"Published message to topic {topic}: {content}");
				return response.Success;
			}
			catch (RpcException ex)
			{
				_logger.LogError($"Failed to publish message: {ex.Message}");
				return false;
			}
		}

		private async Task HandleMessage(Message message)
		{
			// Process the received message
			await Task.Run(() =>
			{
				// Your message processing logic here
				Console.WriteLine($"Processing: {message.Content}");
			});
		}
	}
}
