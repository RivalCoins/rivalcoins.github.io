using RivalCoins.Sdk;
using stellar_dotnet_sdk.responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RivalCoins.Wallet.Web.Client;

public interface IRivalCoinsApp
{
    Task CreateWalletAsync(string password);
    Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync();
    Task<bool> RestoreWalletAsync(string password);
    Task<bool> LoginUserAsync(string password);
    Task<bool> AirDropAsync();
    string GetPublicAddress();
    Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity);
    Task<Balance[]> GetBalancesAsync();
    Network Network { get; }
}
