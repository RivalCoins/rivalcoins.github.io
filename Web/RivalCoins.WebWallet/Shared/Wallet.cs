using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using KeyPair = stellar_dotnet_sdk.KeyPair;
using Network = stellar_dotnet_sdk.Network;
using Server = stellar_dotnet_sdk.Server;

namespace RivalCoins.WebWallet.Shared
{
    public enum StellarManagedNetwork
    {
        Testnet,
        Public
    }


    public record Wallet(
        StellarManagedNetwork StellarManagedNetwork,
        string AccountSecretSeed,
        string HomeDomain)
    {
        public const string MaxTrustLineAmount = "922,337,203,685.4775";

        public static Dictionary<StellarManagedNetwork, Asset> MONEY = new Dictionary<StellarManagedNetwork, Asset>
        {
            { StellarManagedNetwork.Testnet, Asset.CreateNonNativeAsset("MONEY", "GATQKJUFVVI36JYQQC75OOLRFI3BUNBT7ECJKXURNSK34HVIRSOMRRAY") },
            { StellarManagedNetwork.Public, Asset.CreateNonNativeAsset("MONEY", "GA2KQTETIRREL66P64GV6KCVICPULLDVHWJDZSIJKDLIAGBXUCIZ6P6E") }
        };

        public static Dictionary<StellarManagedNetwork, Asset> USDC = new Dictionary<StellarManagedNetwork, Asset>
        {
            { StellarManagedNetwork.Testnet, Asset.CreateNonNativeAsset("USDC", "GCWGBJURIDO4HKAF7EEHDDZO5VEAHGSCOMBR4SNJO3OQI5EQMP54RY7E") },
            { StellarManagedNetwork.Public, Asset.CreateNonNativeAsset("USDC", "GA5ZSEJYB37JRC5AVCIA5MOP4RHTM335X2KGX3IHOJAPP5RE34K4KZVN") }
        };

        public static Dictionary<StellarManagedNetwork, Asset> DOLLAR = new Dictionary<StellarManagedNetwork, Asset>
        {
            { StellarManagedNetwork.Testnet, Asset.CreateNonNativeAsset("DOLLAR", "GCO7B6KEDWOBM5X642ZOTPYTYTTBZIGVGUED4ZSBILJOAU4XB7ISJBFF") },
        };

        public static Asset MoneyTestnet = Asset.Create("credit_alphanum12", "MONEY", "GATQKJUFVVI36JYQQC75OOLRFI3BUNBT7ECJKXURNSK34HVIRSOMRRAY");
        public static Asset MoneyMainnet = Asset.Create("credit_alphanum12", "MONEY", "GA2KQTETIRREL66P64GV6KCVICPULLDVHWJDZSIJKDLIAGBXUCIZ6P6E");

        public static Wallet Testnet = new Wallet(StellarManagedNetwork.Testnet, string.Empty, "test.rivalcoins.io");
        public static Wallet Mainnet = new Wallet(StellarManagedNetwork.Public, string.Empty, "rivalcoins.io");

        public Network NetworkInfo { get; } = StellarManagedNetwork == StellarManagedNetwork.Testnet ? Network.Test() : Network.Public();
        private Lazy<Server> _server;
        public Lazy<Server> Server
        {
            get
            {
                if (_server == null)
                {
                    _server = new Lazy<Server>(() => new Server($"https://horizon{(StellarManagedNetwork == StellarManagedNetwork.Testnet ? "-testnet" : string.Empty)}.stellar.org"));
                }

                return _server;
            }
        }

        private (AccountResponse Account, KeyPair KeyPairWithSeed) _bootstrapperAccount;
        public (AccountResponse Account, KeyPair KeyPairWithSeed) Account
        {
            get
            {
                if (_bootstrapperAccount.Account == null)
                {
                    var keyPairWithSeed = KeyPair.FromSecretSeed(AccountSecretSeed);
                    _bootstrapperAccount = (GetAccountAsync(keyPairWithSeed, this.Server.Value).Result, keyPairWithSeed);
                }

                return _bootstrapperAccount;
            }
        }

        private Transaction _transaction;
        public Transaction Transaction
        {
            get
            {
                if (_transaction == null)
                {
                    _transaction = Transaction.CreateAsync(Account.KeyPairWithSeed, this.Server.Value).Result;
                }

                return _transaction;
            }
        }

        public async Task<Transaction> CreateTransactionAsync()
        {
            return await Transaction.CreateAsync(Account.KeyPairWithSeed, this.Server.Value);
        }

        public static async Task<AccountResponse> GetAccountAsync(KeyPair account, Server server)
        {
            var accountResponse = await server.Accounts.Account(account.AccountId);

            return accountResponse;
        }
    }
}
