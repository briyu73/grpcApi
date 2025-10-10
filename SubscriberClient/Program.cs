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

		// Let it run for a while
		//await Task.Delay(12000);
		//await Task.Delay(12000);
		//await Task.Delay(12000);

		Console.ReadKey();

		Console.WriteLine("Exiting...");
		cts.Cancel(); // Stop subscription
		await subscribeTask;
	}
}