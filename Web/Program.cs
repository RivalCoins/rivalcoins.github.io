using Blazored.LocalStorage;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using RivalCoins.Sdk.Grpc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace RivalCoins.Wallet.Web.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddSingleton(services =>
            {
                var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
                var channel = GrpcChannel.ForAddress("https://wallet.rivalcoins.io", new GrpcChannelOptions { HttpClient = httpClient });
                return new RivalCoinsService.RivalCoinsServiceClient(channel);
            });

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddBlazoredLocalStorage();

            builder.Services.AddScoped<IRivalCoinsApp, RivalCoinsApp>();

            builder.Services.AddMudServices();

            await builder.Build().RunAsync();
        }
    }
}
