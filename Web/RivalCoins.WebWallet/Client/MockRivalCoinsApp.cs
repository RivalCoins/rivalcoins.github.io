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
        
        public MockRivalCoinsApp()
        {
            const string IssuingAccount = "GAXJYZ6GFEGMMD6TO72HGCN5LPMPKJH6SYELXUBVZQ3NADXJBXVR2Q6G";

            _swappableCoins = new[]
            {
                new RivalCoin(
                    "MONEY",
                    Asset.CreateNonNativeAsset("MONEY", IssuingAccount),
                    "This is mock MONEY",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/06/logo-500x500-1.png"),
                new RivalCoin(
                    "Scott Schluter", 
                    Asset.CreateNonNativeAsset("SSchluter", IssuingAccount),
                    "[Rival Coin] Scott Schluter",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/06/scott-schluter.jpg"),
                new RivalCoin(
                    "JB Pritzker",
                    Asset.CreateNonNativeAsset("JBPritzker", IssuingAccount),
                    "[Rival Coin] JB Pritzker",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/jbpritzker.jpg"),
                new RivalCoin(
                    "Darren Bailey",
                    Asset.CreateNonNativeAsset("DBailey", IssuingAccount),
                    "[Rival Coin] Darren Bailey",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/DarrenBailey.jpg"),
                new RivalCoin(
                    "Cheryl Erickson",
                    Asset.CreateNonNativeAsset("CErickson", IssuingAccount),
                    "[Rival Coin] Cheryl Erickson",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/CherylErickson.jpg"),
                new RivalCoin(
                    "Gary Rabine",
                    Asset.CreateNonNativeAsset("GRabine", IssuingAccount),
                    "[Rival Coin] Gary Rabine",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/GaryRabine.jpg"),
                new RivalCoin(
                    "Christopher J. Roper",
                    Asset.CreateNonNativeAsset("CJRoper", IssuingAccount),
                    "[Rival Coin] Christohper J. Roper",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/ChristopherRoper.jpg"),
                new RivalCoin(
                    "Paul Schimpf",
                    Asset.CreateNonNativeAsset("PSchimpf", IssuingAccount),
                    "[Rival Coin] Paul Schimpf",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/PaulSchimpf.jpg"),
                new RivalCoin(
                    "Jesse Sullivan",
                    Asset.CreateNonNativeAsset("JSullivan", IssuingAccount),
                    "[Rival Coin] Jesse Sullivan",
                    0.0,
                    "https://rivalcoins.io/wp-content/uploads/2021/09/JesseSullivan.jpg")
            };

            // Add XLM balance
            _balances.Add(new AssetTypeNative(), 0.0);

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
