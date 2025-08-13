using Grpc.Core;
using GrpcPubSub;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Server.Services
{
	public class PubSubServiceImpl : PubSubService.PubSubServiceBase
	{
		private readonly ConcurrentDictionary<string, List<Channel<Message>>> _topicSubscribers = [];
		private readonly ILogger<PubSubServiceImpl> _logger;

		public PubSubServiceImpl(ILogger<PubSubServiceImpl> logger)
		{
			_logger = logger;
		}

		public override async Task Subscribe(
				SubscribeRequest request,
				IServerStreamWriter<Message> responseStream,
				ServerCallContext context)
		{
			var channel = Channel.CreateUnbounded<Message>();
			var topic = request.Topic;

			// Add subscriber to topic
			_topicSubscribers.AddOrUpdate(
					topic,
					new List<Channel<Message>> { channel },
					(key, existing) =>
					{
						lock (existing)
						{
							existing.Add(channel);
						}
						return existing;
					});

			_logger.LogInformation($"Client subscribed to topic: {topic}");

			try
			{
				// Send messages to client as they arrive
				await foreach (var message in channel.Reader.ReadAllAsync(context.CancellationToken))
				{
					await responseStream.WriteAsync(message);
				}
			}
			catch (OperationCanceledException)
			{
				_logger.LogInformation($"Client unsubscribed from topic: {topic}");
			}
			finally
			{
				// Clean up subscription
				if (_topicSubscribers.TryGetValue(topic, out var subscribers))
				{
					lock (subscribers)
					{
						subscribers.Remove(channel);
						if (subscribers.Count == 0)
						{
							_topicSubscribers.TryRemove(topic, out _);
						}
					}
				}
				channel.Writer.Complete();
			}
		}

		public override Task<PublishResponse> Publish(
				PublishRequest request,
				ServerCallContext context)
		{
			var message = new Message
			{
				Topic = request.Topic,
				Content = request.Content,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
			};

			var publishedCount = 0;

			if (_topicSubscribers.TryGetValue(request.Topic, out var subscribers))
			{
				lock (subscribers)
				{
					foreach (var channel in subscribers.ToList())
					{
						if (channel.Writer.TryWrite(message))
						{
							publishedCount++;
						}
					}
				}
			}

			_logger.LogInformation($"Published message to {publishedCount} subscribers on topic: {request.Topic}");

			return Task.FromResult(new PublishResponse { Success = true });
		}
	}
}
