using RivalCoins.Sdk;
using stellar_dotnet_sdk.responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RivalCoins.Wallet.Web.Client;

public interface IRivalCoinsApp
{
    Task CreateWalletAsync(string password, StellarManagedNetwork network);
    Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync(StellarManagedNetwork network);
    Task<bool> RestoreWalletAsync(string password, StellarManagedNetwork network);
    Task<bool> LoginUserAsync(string password, StellarManagedNetwork network);
    Task<bool> AirDropAsync();
    string GetPublicAddress();
    Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity);
    Task<Balance[]> GetBalancesAsync();
}
