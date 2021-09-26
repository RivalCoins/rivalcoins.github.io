using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RivalCoins.WebWallet.Shared;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.WebWallet.Client
{
    public class MockRivalCoinsApp : IRivalCoinsApp
    {
        private readonly RivalCoin[] _swappableCoins;
        private readonly Dictionary<Asset, double> _balances = new Dictionary<Asset, double>();
        private readonly RivalCoin _money;
        private readonly Asset _xlm = new AssetTypeNative();
        
        public MockRivalCoinsApp()
        {
            const string IssuingAccount = "GAXJYZ6GFEGMMD6TO72HGCN5LPMPKJH6SYELXUBVZQ3NADXJBXVR2Q6G";

            _money = new RivalCoin(
                "MONEY",
                Asset.CreateNonNativeAsset("MONEY", IssuingAccount),
                "This is mock MONEY",
                0.0,
                "https://rivalcoins.io/wp-content/uploads/2021/06/logo-500x500-1.png");

            _swappableCoins = new[]
            {
                _money,
                new RivalCoin(
                    "Asset A", 
                    Asset.CreateNonNativeAsset("AssetA", IssuingAccount),
                    "[Rival Coin] Asset A",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/06/logo-500x500-1.png"),
                new RivalCoin(
                    "Asset B",
                    Asset.CreateNonNativeAsset("AssetB", IssuingAccount),
                    "[Rival Coin] Asset B",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/06/logo-500x500-1.png")
            };

            _balances.Add(_xlm, 0.0);

            foreach (var swappableCoin in _swappableCoins)
            {
                _balances.Add(swappableCoin.Asset, 0.0);
            }
        }

        public Task CreateWalletAsync(string password, StellarManagedNetwork network)
        {
            return Task.CompletedTask;
        }

        public Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync(StellarManagedNetwork network)
        {
            return Task.FromResult((IEnumerable<RivalCoin>)_swappableCoins);
        }

        public Task<bool> RestoreWalletAsync(string password, StellarManagedNetwork network)
        {
            return Task.FromResult(true);
        }

        public Task<bool> LoginUserAsync(string password, StellarManagedNetwork network)
        {
            return Task.FromResult(true);
        }

        public Task<bool> AirDropAsync()
        {
            _balances[_swappableCoins.First(coin => coin.Asset.Code() == "MONEY").Asset] += 100.0;

            return Task.FromResult(true);
        }

        public string GetPublicAddress()
        {
            return "GBLSRZ62R4N4HNCK2A2VO5ZTGRMK3DCGICJCYO7NAAWBGFLHH5PS4IEW";
        }

        public Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity)
        {
            _balances[swapOut.Asset] -= quantity;
            _balances[swapIn.Asset] += quantity;

            return Task.FromResult(true);
        }

        public Task<Balance[]> GetBalancesAsync()
        {
            return Task.FromResult(_balances.Select(balance => GetBalance(balance.Key, balance.Value)).ToArray());
        }

        private static Balance GetBalance(Asset asset, double balance)
        {
            var foo = asset.CanonicalName();
            return new Balance(
                asset is AssetTypeNative ? "native" : "alphanum12",
                asset is AssetTypeNative ? string.Empty : asset.Code(),
                asset is AssetTypeNative ? string.Empty : asset.Issuer(),
                balance.ToString(),
                "900000000.0",
                "0.0",
                "0.0",
                true,
                true);
        }
    }
}
