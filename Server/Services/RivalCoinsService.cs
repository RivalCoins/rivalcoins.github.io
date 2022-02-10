using Grpc.Core;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;

namespace RivalCoins.Server.Services;

public class RivalCoinsService : Sdk.Grpc.RivalCoinsService.RivalCoinsServiceBase
{
    private readonly Wallet _airDropWallet;

    public RivalCoinsService(Wallet airDropWallet)
    {
        _airDropWallet = airDropWallet;
    }

    public override async Task<Success> SubmitAirDropTransaction(SignedTransaction request, ServerCallContext context)
    {
        var tx = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(request.Xdr);

        tx.Sign(_airDropWallet.KeyPairWithSecretSeed, _airDropWallet.NetworkInfo);

        var xdr = tx.ToEnvelopeXdrBase64();
        Console.WriteLine(xdr);
        Console.WriteLine();
        Console.WriteLine();

        var response = await _airDropWallet.Server.SubmitTransaction(tx);

        if (!response.IsSuccess())
        {
            Console.WriteLine($"Failure Message: {response.ResultXdr}");
        }

        return new Success() { Success_ = response.IsSuccess() };
    }

    public override async Task<Sdk.Grpc.Transaction> CreateAirDropTransaction(AirDropRequest request, ServerCallContext context)
    {
        const string AirDropAmount = "1000.0";

        // user will pay the air drop transaction fee

        //TODO validate transaction ability to execute transaction

        var airDropTransaction = await Sdk.Transaction.CreateAsync(KeyPair.FromAccountId(request.RecipientAddress), _airDropWallet.Server);
        var usdc = Wallet.USDC[_airDropWallet.Network];

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
            new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(usdc)).Build(),
            _airDropWallet.KeyPairWithSecretSeed);

        // send air drop
        airDropTransaction.AddOperation(
            new PaymentOperation.Builder(
                KeyPair.FromAccountId(request.RecipientAddress),
                usdc,
                AirDropAmount)
                .SetSourceAccount(_airDropWallet.KeyPairWithSecretSeed)
                .Build(),
            _airDropWallet.KeyPairWithSecretSeed);


        return new Sdk.Grpc.Transaction() { UnsignedXdr = airDropTransaction.GetXdr() };
    }
}
