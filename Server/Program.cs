using Microsoft.Extensions.Configuration.Memory;
using RivalCoins.Sdk;
using RivalCoins.Server.Services;

namespace RivalCoins.Server;

public class Program
{
    const Network TargetNetwork = Network.Local;
    const string AirDropAccountSeed = "";
    const string AzureFormRecognizerApiKey = "";

    const string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Additional configuration is required to successfully run gRPC on macOS.
        // For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

        // Add services to the container.
        builder.Services.AddGrpc();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: MyAllowSpecificOrigins,
                                builder =>
                                {
                                    builder
                                    .AllowAnyOrigin()
                                    .AllowAnyHeader()
                                    .AllowAnyMethod()
                                    .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding"); ;
                                });
        });

        builder.Services.AddSingleton(_ =>
        {
            var airDropWallet = Wallet.Default[TargetNetwork] with { AccountSecretSeed = AirDropAccountSeed };
            airDropWallet.InitializeAsync().Wait();

            return airDropWallet;
        });

        var configValues = new Dictionary<string, string>
        {
            { "network", TargetNetwork.ToString() },
            { "AzureFormRecognizerApiKey", AzureFormRecognizerApiKey }
        };
        builder.Configuration.AddInMemoryCollection(configValues);

        var app = builder.Build();

        app.UseGrpcWeb();
        app.UseCors();

        // Configure the HTTP request pipeline.

        app.MapGrpcService<RivalCoinsService>()
            .EnableGrpcWeb()
            .RequireCors(MyAllowSpecificOrigins);

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

        app.Run();
    }
}