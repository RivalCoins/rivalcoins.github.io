using Blazored.LocalStorage;
using Microsoft.JSInterop;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace RivalCoins.Wallet.Web.Client;

// Cannot use stellar_dotnet_sdk.KeyPair because it requires
// System.Security.Cryptography.Csp, which is not supported on Blazor WebAssembly
public record KeyPairBasic(string PublicKey, string PrivateKey);

public record RivalCoin(string Name, Asset Asset, string Description, double Quantity, string IconUri);

public class RivalCoinsApp : IRivalCoinsApp
{
    private readonly ILocalStorageService _localStorage;
    private readonly IJSRuntime _js;
    private readonly RivalCoinsService.RivalCoinsServiceClient _serverClient;
    private readonly Sdk.Network _network;

    private (Sdk.Wallet Wallet, KeyPairBasic Account) _wallet;

    public RivalCoinsApp(
        ILocalStorageService localStorage,
        IJSRuntime javaScriptRuntime,
        RivalCoinsService.RivalCoinsServiceClient serverClient,
        IConfiguration config)
    {
        _localStorage = localStorage;
        _js = javaScriptRuntime;
        _serverClient = serverClient;

        _network = (Sdk.Network)Enum.Parse(typeof(Sdk.Network), config.GetValue<string>("network"));
    }

    public Sdk.Network Network => _network;

    #region Disposal

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);

        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    private bool _disposed = false;
    protected void Dispose(bool explicitDisposal)
    {
        if (_disposed)
        {
            return;
        }

        // disposed managed objects
        if (explicitDisposal)
        {
            if (_wallet.Wallet != null)
            {
                _wallet.Wallet.Dispose();
            }
        }

        _disposed = true;
    }

    #endregion Disposal

    private async Task CreateWalletAsync(string password)
    {
        _wallet = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPair"), _network);

        // store encrypted secret seed in local storage
        var encryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmEncrypt", _wallet.Account.PrivateKey, password);
        await _localStorage.SetItemAsStringAsync("SecretSeed", encryptedSecretSeed);
    }

    public async Task<IEnumerable<RivalCoin>> GetSwappableCoinsAsync()
    {
        try
        {
            var website = Sdk.Wallet.GetRivalCoinsWebsite(_network);
            var sanity = GetRivalCoinsAsync(_network);
            var rivalCoins = await Sdk.Wallet.GetRivalCoinsAsync(_network);

            return rivalCoins.Select(rivalCoin => new RivalCoin(rivalCoin.Name, rivalCoin.Asset, rivalCoin.Description, 0.0, rivalCoin.ImageUri)).ToList();
        }
        catch (Exception ex)
        {
            ;
        }

        return null;
    }

    public static async Task<List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>> GetRivalCoinsAsync(Sdk.Network network)
    {
        using var http = new HttpClient();
        await using var rivalCoinsToml = await http.GetStreamAsync($"{Sdk.Wallet.GetRivalCoinsWebsite(network)}/.well-known/stellar.toml");
        using var rivalCoinsTomlStream = new StreamReader(rivalCoinsToml);
        var rivalCoins = new List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>();

        return rivalCoins;
    }

    public async Task<bool> RestoreWalletAsync(string password)
    {
        if (await _localStorage.ContainKeyAsync("SecretSeed"))
        {
            var encryptedSecretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
            var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", encryptedSecretSeed, password);

            _wallet = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), _network);

            return _wallet.Wallet.IsInitialized;
        }

        return false;
    }

    public async Task<bool> LoginUserAsync(string password)
    {
        // log in user if they are not already logged in and a wallet can be restored
        if (await _localStorage.ContainKeyAsync("SecretSeed"))
        {
            var secretSeed = await _localStorage.GetItemAsStringAsync("SecretSeed");
            var decryptedSecretSeed = await _js.InvokeAsync<string>("aesGcmDecrypt", secretSeed, password);

            _wallet = await RestoreWalletInternalAsync(await _js.InvokeAsync<string>("createKeyPairFromSeed", decryptedSecretSeed), _network);

            return true;
        }
        else
        {
            await this.CreateWalletAsync(password);
            return true;
        }

        return false;
    }

    public async Task<bool> AirDropAsync()
    {
        try
        {
            var tx = await _serverClient.CreateAirDropTransactionAsync(new AirDropRequest() { RecipientAddress = _wallet.Account.PublicKey });

            var signedTx = await this.SignTransactionAsync(tx.UnsignedXdr);

            return (await _serverClient.SubmitAirDropTransactionAsync(new SignedTransaction() { Xdr = signedTx })).Success_;
        }
        catch (Exception ex)
        {
            ;
        }

        return false;
    }

    public async Task<string> SignTransactionAsync(string xdr) => await _js.InvokeAsync<string>("signTransaction", xdr, _wallet.Wallet.NetworkInfo.NetworkPassphrase, _wallet.Account.PrivateKey);

    public async Task SubmitTransactionAsync(string xdr) => await _js.InvokeVoidAsync("submitTransaction", xdr, _wallet.Wallet.NetworkInfo.NetworkPassphrase, _wallet.Account.PrivateKey, Sdk.Wallet.GetHorizonUri(_wallet.Wallet.Network));
 
    public string GetPublicAddress() => _wallet.Account?.PublicKey;

    public async Task<bool> SwapAysnc(RivalCoin swapOut, RivalCoin swapIn, double quantity)
    {
        var paths = await _wallet.Wallet.Server.PathStrictReceive
            .SourceAssets(new[] { swapOut.Asset })
            .DestinationAsset(swapIn.Asset)
            .DestinationAmount(quantity.ToString())
            .Execute();

        var pathAsset = paths.Records.First().Path.FirstOrDefault() ?? swapIn.Asset;

        await _js.InvokeVoidAsync(
            "swap",
            swapOut.Asset.Code(),
            swapOut.Asset.Issuer(),
            swapIn.Asset.Code(),
            swapIn.Asset.Issuer(),
            quantity.ToString(),
            pathAsset.Code(),
            pathAsset.Issuer(),
            _wallet.Wallet.NetworkInfo.NetworkPassphrase,
            _wallet.Account.PrivateKey,
            Sdk.Wallet.GetHorizonUri(_wallet.Wallet.Network));

        return true;
    }

    public async Task<Balance[]> GetBalancesAsync()
    {
        var account = await _wallet.Wallet.Server.Accounts.Account(_wallet.Account.PublicKey);

        return account.Balances;
    }

    private static async Task<(Sdk.Wallet Wallet, KeyPairBasic Account)> RestoreWalletInternalAsync(string walletKeyPairInfo, Sdk.Network network)
    {
        const int PublicKeyIndex = 1;
        const int SecretSeedIndex = 0;

        var walletKeyPair = new KeyPairBasic(walletKeyPairInfo.Split(':')[PublicKeyIndex], walletKeyPairInfo.Split(':')[SecretSeedIndex]);
        var wallet = Sdk.Wallet.Default[network] with { AccountSecretSeed = walletKeyPair.PrivateKey };
        var accountExists = false;

        // if the account does not exit on the system, create it
        try
        {
            _ = await wallet.Server.Accounts.Account(walletKeyPair.PublicKey);
            accountExists = true;
        }
        catch (Exception)
        {
        }

        // initialize wallet
        if(accountExists)
        {
            await wallet.InitializeAsync(walletKeyPair.PublicKey);
        }

        return (wallet, walletKeyPair);
    }

    public async Task<(bool Success, string Message)> GetTaxContributionHonorAsync(Stream receipt)
    {
        var result = await _serverClient.ReceiveTaxContributionHonorAsync(new TaxContributionInfo()
        {
            AccountId = _wallet.Account.PublicKey,
            Receipt = await Google.Protobuf.ByteString.FromStreamAsync(receipt)
        });

        await this.SubmitTransactionAsync(result.SignedXdr);

        return (result.Success.Success_, result.Success.Message);
    }
}
