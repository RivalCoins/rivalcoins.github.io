using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using RivalCoins.WebWallet.Shared;
using RivalCoins.WebWallet.Shared.Grpc;
using stellar_dotnet_sdk;
using Transaction = RivalCoins.WebWallet.Shared.Grpc.Transaction;

namespace RivalCoins.WebWallet.Server
{
    public class RivalCoinsService : Shared.Grpc.RivalCoinsService.RivalCoinsServiceBase
    {
        private const string AirDropAccountSeed = "<CHANGE ME>";

        private readonly Wallet _airDropWallet;

        public RivalCoinsService()
        {
            _airDropWallet = Wallet.Testnet with { AccountSecretSeed = AirDropAccountSeed };
        }

        public override async Task<Success> SubmitAirDropTransaction(SignedTransaction request, ServerCallContext context)
        {
            var tx = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(request.Xdr);

            tx.Sign(_airDropWallet.Account.KeyPairWithSeed, _airDropWallet.NetworkInfo);

            var xdr = tx.ToEnvelopeXdrBase64();
            Console.WriteLine(xdr);
            Console.WriteLine();
            Console.WriteLine();

            var response = await _airDropWallet.Server.Value.SubmitTransaction(tx);

            if (!response.IsSuccess())
            {
                Console.WriteLine($"Failure Message: {response.ResultXdr}");
            }

            return new Success() { Success_ = response.IsSuccess() };
        }

        public override async Task<Transaction> CreateAirDropTransaction(AirDropRequest request, ServerCallContext context)
        {
            const string AirDropAmount = "1000.0";

            // user will pay the air drop transaction fee

            //TODO validate transaction ability to execute transaction

            var airDropTransaction = await Shared.Transaction.CreateAsync(KeyPair.FromAccountId(request.RecipientAddress), _airDropWallet.Server.Value);
            var dollar = Wallet.USDC[_airDropWallet.StellarManagedNetwork];

            // create trustline
            //      Remember, the TRANSACTION source account is the user's account, thus any
            //      OPERATION without a source account explicitly set will default to the
            //      transaction's source account, which is the user's account.  Thus, a 
            //      trustline is being made on the user's account.
            //      
            //      The second parameter really should be null, but the API does not allow it.
            //      The value is only used when signing the transaction.  I'm intentionally
            //      setting it to the Air Drop account, since that's redundant with
            //      the payment operation and will thus have no real effect.
            airDropTransaction.AddOperation(
                new ChangeTrustOperation.Builder(dollar).Build(),
                _airDropWallet.Account.KeyPairWithSeed);

            // send air drop
            airDropTransaction.AddOperation(
                new PaymentOperation.Builder(
                    KeyPair.FromAccountId(request.RecipientAddress),
                    dollar,
                    AirDropAmount)
                    .SetSourceAccount(_airDropWallet.Account.Account.KeyPair)
                    .Build(),
                _airDropWallet.Account.KeyPairWithSeed);

            
            return new Transaction() { UnsignedXdr = airDropTransaction.GetXdr()};
        }
    }
}
