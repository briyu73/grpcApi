using Client1.Services;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcPubSub;

public class ClientProgram
{
	public static async Task Main(string[] args)
	{
		using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
		var logger = loggerFactory.CreateLogger<PubSubClient>();

		var client = new PubSubClient("http://localhost:55551", logger);

		// Start subscriber in background task
		var cts = new CancellationTokenSource();
		var subscribeTask = Task.Run(() => client.SubscribeAsync("news", cts.Token));

		// Publish some messages
		await Task.Delay(1000); // Wait a bit for subscription to establish

		await client.PublishAsync("news", "Breaking: gRPC PubSub is working!");
		await Task.Delay(1000);
		await client.PublishAsync("news", "Weather update: Sunny today");
		await Task.Delay(1000);
		await client.PublishAsync("sports", "Game results: Team A wins!");

		// Let it run for a while
		await Task.Delay(5000);

		cts.Cancel(); // Stop subscription
		await subscribeTask;

		Console.ReadKey();
		Console.WriteLine("Eiting..");
	}
}