// Server startup
using Server.Services;

public class Program
{
	public static void Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Services.AddGrpc();
		builder.Services.AddLogging();
		builder.Services.AddSingleton<PubSubServiceImpl>();

		//builder.WebHost.UseKestrel();
		//builder.WebHost.ConfigureKestrel((context, serverOptions) => serverOptions.ListenLocalhost(55551));

		var app = builder.Build();

		app.MapGrpcService<PubSubServiceImpl>();

		app.Run("http://localhost:55551");
		//app.Run();
	}
}