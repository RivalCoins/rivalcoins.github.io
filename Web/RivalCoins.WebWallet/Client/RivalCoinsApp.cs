using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using Microsoft.JSInterop;
using RivalCoins.WebWallet.Shared;
using RivalCoins.WebWallet.Shared.Grpc;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using Tommy;

namespace RivalCoins.WebWallet.Client
{
    // Cannot use stellar_dotnet_sdk.KeyPair because it requires
    // System.Security.Cryptography.Csp, which is not supported on Blazor WebAssembly
    public record KeyPairBasic(string PublicKey, string PrivateKey);

    public record RivalCoin(string Name, Asset Asset, string Description, double Quantity, string IconUri);

    public class RivalCoinsApp : IRivalCoinsApp
    {
        private readonly ILocalStorageService _localStorage;
        private readonly IJSRuntime _js;
        private readonly RivalCoinsService.RivalCoinsServiceClient _serverClient;
        private readonly HttpClient _http;

        private bool _userLoggedIn;
        private (Wallet Wallet, KeyPairBasic Account) _wallet;

        public RivalCoinsApp(
            ILocalStorageService localStorage, 
            IJSRuntime javaScriptRuntime,
            RivalCoinsService.RivalCoinsServiceClient serverClient,
            HttpClient http)
        {
            _localStorage = localStorage;
            _js = javaScriptRuntime;
            _serverClient = serverClient;
            _http = http;
        }

        public async Task CreateWalletAsync(string password, StellarManagedNetwork network)
        {
            // We don't use .NET API because System.Security.Cryptography.Csp is not supported on this platform.

            // generate key pair
            _wallet = RestoreWallet(await _js.InvokeAsync<string>("createKeyPair"), network);

            // store encrypted secret seed in local storage
            var encryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmEncrypt", _wallet.Account.PrivateKey, password);
            await _localStorage.SetItemAsStringAsync("SecretSeed", encryptedSecretSeed);

            // fund account
            if(network == StellarManagedNetwork.Testnet)
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync($"https://friendbot.stellar.org?addr={_wallet.Account.PublicKey}");
            }
        }

        public async Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync(StellarManagedNetwork network)
        {
            await using var rivalCoinsToml = await _http.GetStreamAsync($"https://{(network == StellarManagedNetwork.Testnet ? "test." : string.Empty)}rivalcoins.io/.well-known/stellar{(network == StellarManagedNetwork.Testnet ? "-test" : string.Empty)}.toml");
            using var rivalCoinsTomlStream = new StreamReader(rivalCoinsToml);
            var swappableCoins = new List<RivalCoin>();

            var table = TOML.Parse(rivalCoinsTomlStream);
            foreach (TomlNode tomlNode in table["CURRENCIES"])
            {
                var rivalCoin = new RivalCoin(
                    tomlNode["name"].AsString.Value,
                    Asset.CreateNonNativeAsset(tomlNode["code"], tomlNode["issuer"]),
                    tomlNode["desc"],
                    0.0,
                    tomlNode["image"]);

                swappableCoins.Add(rivalCoin);
            }

            return swappableCoins;
        }

        public async Task<bool> RestoreWalletAsync(string password, StellarManagedNetwork network)
        {
            if (await _localStorage.ContainKeyAsync("SecretSeed"))
            {
                var encryptedSecretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
                var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", encryptedSecretSeed, password);

                _wallet = RestoreWallet(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), network);

                return true;
            }

            return false;
        }

        public async Task<bool> LoginUserAsync(string password, StellarManagedNetwork network)
        {
            // log in user if they are not already logged in and a wallet can be restored
            if (!_userLoggedIn && await _localStorage.ContainKeyAsync("SecretSeed"))
            {
                var secretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
                var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", secretSeed, password);

                _wallet = RestoreWallet(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), network);

                return true;
            }

            return false;
        }

        public async Task<bool> AirDropAsync()
        {
            var tx = await _serverClient.CreateAirDropTransactionAsync(new AirDropRequest() { RecipientAddress = _wallet.Account.PublicKey });

            var signedTx = await _js.InvokeAsync<string>("signTransaction", tx.UnsignedXdr, _wallet.Wallet.NetworkInfo.NetworkPassphrase, _wallet.Account.PrivateKey);

            return (await _serverClient.SubmitAirDropTransactionAsync(new SignedTransaction() { Xdr = signedTx })).Success_;
        }

        public string GetPublicAddress() => _wallet.Account.PublicKey;

        public async Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity)
        {
            await _js.InvokeVoidAsync(
                "swap",
                swapOut.Asset.Code(),
                swapOut.Asset.Issuer(),
                swapIn.Asset.Code(),
                swapIn.Asset.Issuer(),
                quantity.ToString(),
                _wallet.Wallet.NetworkInfo.NetworkPassphrase,
                _wallet.Account.PrivateKey);

            return true;
        }

        public async Task<Balance[]> GetBalancesAsync()
        {
            var account = await _wallet.Wallet.Server.Value.Accounts.Account(_wallet.Account.PublicKey);

            return account.Balances;
        }

        private static (Wallet Wallet, KeyPairBasic Account) RestoreWallet(string walletKeyPairInfo, StellarManagedNetwork network)
        {
            const int PublicKeyIndex = 1;
            const int SecretSeedIndex = 0;

            var walletKeyPair = new KeyPairBasic(walletKeyPairInfo.Split(':')[PublicKeyIndex], walletKeyPairInfo.Split(':')[SecretSeedIndex]);
            var baseNetwork = network == StellarManagedNetwork.Testnet ? Wallet.Testnet : Wallet.Mainnet;

            return (baseNetwork with { AccountSecretSeed = walletKeyPair.PrivateKey }, walletKeyPair);
        }
    }
}
