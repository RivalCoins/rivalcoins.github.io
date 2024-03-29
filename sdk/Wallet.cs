﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.requests;
using stellar_dotnet_sdk.responses;
using Tommy;
using KeyPair = stellar_dotnet_sdk.KeyPair;
using Server = stellar_dotnet_sdk.Server;

namespace RivalCoins.Sdk
{
    public enum Network
    {
        Testnet,
        Mainnet,
        Local,
        Demo
    }

    public record Wallet(
        Network Network,
        string AccountSecretSeed,
        string HomeDomain) : IDisposable
    {
        public static Dictionary<Network, Wallet> Default = new Dictionary<Network, Wallet>
        {
            { Network.Testnet,  new Wallet(Network.Testnet, string.Empty, "test.rivalcoins.money") },
            { Network.Demo,     new Wallet(Network.Demo, string.Empty, "demo.rivalcoins.money") },
            { Network.Local,    new Wallet(Network.Local, string.Empty, "local.rivalcoins.money") },
            { Network.Mainnet,   new Wallet(Network.Mainnet, string.Empty, "rivalcoins.money")},
        };

        public stellar_dotnet_sdk.Network NetworkInfo { get; private set; }

        public bool IsInitialized { get; private set; }

        private Server _server;
        public Server Server
        {
            get
            {
                if (_server == null)
                {
                    _server = new Server(GetHorizonUri(this.Network));
                }

                return _server;
            }
        }

        public static string GetHorizonUri(Network network) => network switch
        {
            Network.Testnet => "https://horizon-testnet.stellar.org",
            Network.Mainnet => "https://horizon.stellar.org",
            Network.Local => "http://localhost:8000",
            Network.Demo => "https://rivalcoins-stellar-horizon-rzu5x.ondigitalocean.app",
            _ => throw new NotImplementedException()
        };

        public KeyPair KeyPairWithSecretSeed => KeyPair.FromSecretSeed(this.AccountSecretSeed);

        public AccountResponse Account { get; private set; }

        public Transaction Transaction { get; private set; }

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
                if (this.Server != null)
                {
                    this.Server.Dispose();
                }
            }

            _disposed = true;
        }

        private async Task InitializeInternalAsync(string accountId)
        {
            if (this.IsInitialized)
            {
                throw new Exception("Already initialized!");
            }

            if (Wallet.Default.Values.Any(defaultWallet => this == defaultWallet))
            {
                throw new Exception("You cannot initialize a default wallet.  You may only derive a default wallet, eg var myWallet = Wallet.Testnet with { AccountSecretSeed = 'My Secret Seed' }.");
            }

            // KeyPair uses NSec.Cryptography.Sodium.InitializeCore, which is not supported on Blazor WebAssembly.
            // We will initialize as much as we can while avoiding this library.
            var canUseCryptography = CanUseCryptography();

            // get account info
            this.Account = await this.Server.Accounts.Account(canUseCryptography && !string.IsNullOrWhiteSpace(this.AccountSecretSeed) ? this.KeyPairWithSecretSeed.AccountId : accountId);

            // initialize transaction engine
            if (canUseCryptography && !string.IsNullOrWhiteSpace(this.AccountSecretSeed))
            {
                this.Transaction = await Transaction.CreateAsync(this.KeyPairWithSecretSeed, this.Server);
            }

            // get network info
            var responseHandler = new ResponseHandler<RootResponse>();
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(GetHorizonUri(this.Network));
            var rootResponse = await responseHandler.HandleResponse(response);

            this.NetworkInfo = new stellar_dotnet_sdk.Network(rootResponse.NetworkPassphrase);

            this.IsInitialized = true;
        }

        public async Task InitializeAsync(string accountId)
        {
            await this.InitializeInternalAsync(accountId);
        }

        public async Task InitializeAsync()
        {
            if (!CanUseCryptography())
            {
                throw new Exception($"This platform does not support cryptography eg 'NSec.Cryptography.Sodium.InitializeCore', please use {nameof(InitializeAsync)}(string) which does not use cryptography functions.");
            }

            await this.InitializeInternalAsync(string.Empty);
        }

        public static async Task CreateAccountAsync(string accountId, Network network)
        {
            using var httpClient = new HttpClient();

            var friendbotHost = network switch
            {
                Network.Testnet => "https://friendbot.stellar.org",
                Network.Demo => "https://rivalcoins-stellar-horizon-rzu5x.ondigitalocean.app/friendbot",
                Network.Local => "http://localhost:8000/friendbot",
                _ => throw new NotImplementedException(),
            };

            var response = await httpClient.GetAsync($"{friendbotHost}?addr={accountId}");
        }

        public static double GetMinimumBalance(double baseReserve, int numEntries, int numSponsoringEntries, int numSponsoredEntries)
            => (2 + numEntries + numSponsoringEntries - numSponsoredEntries) * baseReserve;

        public static string GetRivalCoinsWebsite(Network network) => network switch
        {
            Network.Testnet => "http://localhost",
            Network.Demo => "https://demo.rivalcoins.money",
            Network.Local => "http://localhost",
            Network.Mainnet => "https://rivalcoins.money"
        };

        public static async Task<List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>> GetRivalCoinsAsync(Network network)
        {
            using var http = new HttpClient();
            var rivalCoins = new List<(string Name, AssetTypeCreditAlphaNum Asset, string Description, string ImageUri)>();
            using var reader = new StringReader(await http.GetStringAsync($"{GetRivalCoinsWebsite(network)}/.well-known/stellar.toml"));
            var table = TOML.Parse(reader);

            foreach (TomlNode currency in table["CURRENCIES"])
            {
                var rivalCoin = (
                    currency["name"],
                    (AssetTypeCreditAlphaNum)Asset.CreateNonNativeAsset(currency["code"], currency["issuer"]),
                    currency["desc"],
                    currency["image"]);

                rivalCoins.Add(rivalCoin);
            }

            return rivalCoins;
        }

        public static string GetRivalCoinsServerHost(Network network) => network switch
        {
            Network.Local => "https://localhost:7123",
            Network.Mainnet => "https://wallet.rivalcoins.io",
            Network.Testnet => "https://localhost:7123",
            _ => throw new NotImplementedException()
        };

        private static bool CanUseCryptography()
        {
            var canUseCryptography = false;

            try
            {
                _ = KeyPair.Random();
                canUseCryptography = true;
            }
            catch (Exception) { }

            return canUseCryptography;
        }
    }
}
