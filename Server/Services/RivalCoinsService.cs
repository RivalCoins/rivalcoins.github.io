using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Google.Protobuf;
using Grpc.Core;
using RivalCoins.Sdk;
using RivalCoins.Sdk.Grpc;
using stellar_dotnet_sdk;

namespace RivalCoins.Server.Services;

public class RivalCoinsService : Sdk.Grpc.RivalCoinsService.RivalCoinsServiceBase
{
    private readonly Wallet _airDropWallet;
    private readonly string _azureFormRecognizerApiKey;

    private AssetTypeCreditAlphaNum _playUSA;
    private AssetTypeCreditAlphaNum _playMONEY;

    public RivalCoinsService(Wallet airDropWallet, IConfiguration configuration)
    {
        _airDropWallet = airDropWallet;
        _azureFormRecognizerApiKey = configuration.GetValue<string>("AzureFormRecognizerApiKey");
    }

    private static double UsaPricedInMoney { get; } = GetMoneyPrice(330L * 1000L * 1000L, 30L * 1000L * 1000L * 1000L * 1000L);

    private static double GetMoneyPrice(long currencyTargetMarketPopulation, long currencySupply)
    {
        const long HumanPopulation = 8L * 1000L * 1000L * 1000L;
        const long MoneySupply = 9600L * 1000L * 1000L * 1000L;

        var currencyTargetMarketPercentageOfWorld = currencyTargetMarketPopulation / (double)HumanPopulation;
        var moneyAllocationToCurrencyTargetMarket = currencyTargetMarketPercentageOfWorld * MoneySupply;

        return moneyAllocationToCurrencyTargetMarket / currencySupply;
    }

    private async Task InitializeWalletsAsync()
    {
        if(!_airDropWallet.IsInitialized)
        {
            await _airDropWallet.InitializeAsync();
        }

        if(_playUSA == null || _playMONEY == null)
        {
            var rivalCoins = await Wallet.GetRivalCoinsAsync(_airDropWallet.Network);

            _playUSA = rivalCoins.FirstOrDefault(rivalCoin => rivalCoin.Asset.Code == "PlayUSA").Asset;
            _playMONEY = rivalCoins.FirstOrDefault(rivalCoin => rivalCoin.Asset.Code == "PlayMONEY").Asset;
        }
    }

    public override async Task<TaxContributionResponse> ReceiveTaxContributionHonor(TaxContributionInfo request, ServerCallContext context)
    {
        await this.InitializeWalletsAsync();

        string endpoint = "https://receipt-tax-reader.cognitiveservices.azure.com";
        AzureKeyCredential credential = new AzureKeyCredential(_azureFormRecognizerApiKey);
        DocumentAnalysisClient client = new DocumentAnalysisClient(new Uri(endpoint), credential);

        var receiptBytes = new byte[request.Receipt.Length];
        request.Receipt.CopyTo(receiptBytes, 0);
       
        using var receiptStream = new MemoryStream(receiptBytes);
        AnalyzeDocumentOperation operation = await client.StartAnalyzeDocumentAsync("prebuilt-receipt", receiptStream);

        await operation.WaitForCompletionAsync();

        AnalyzeResult result = operation.Value;

        for (int i = 0; i < result.Documents.Count; i++)
        {
            Console.WriteLine($"Document {i}:");

            AnalyzedDocument document = result.Documents[i];

            if (document.Fields.TryGetValue("TotalTax", out DocumentField? totalTaxField))
            {
                if (totalTaxField.ValueType == DocumentFieldType.Double)
                {
                    double totalTax = totalTaxField.AsDouble();
                    Console.WriteLine($"Total Tax: '{totalTax}', with confidence {totalTaxField.Confidence}");

                    var airDropTx = await Sdk.Transaction.CreateAsync(KeyPair.FromAccountId(request.AccountId), _airDropWallet.Server);

                    // create trustline
                    //      Remember, the TRANSACTION source account is the user's account, thus any
                    //      OPERATION without a source account explicitly set will default to the
                    //      transaction's source account, which is the user's account.  Thus, a 
                    //      trustline is being made on the user's account.
                    airDropTx.AddOperation(
                        new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(_playUSA)).Build(),
                        null);

                    airDropTx.AddOperation(new PaymentOperation.Builder(
                        KeyPair.FromAccountId(request.AccountId),
                        _playUSA,
                        totalTax.ToString("N7"))
                        .SetSourceAccount(_airDropWallet.KeyPairWithSecretSeed)
                        .Build(),
                        _airDropWallet.KeyPairWithSecretSeed);

                    // create trustline
                    //      Remember, the TRANSACTION source account is the user's account, thus any
                    //      OPERATION without a source account explicitly set will default to the
                    //      transaction's source account, which is the user's account.  Thus, a 
                    //      trustline is being made on the user's account.
                    airDropTx.AddOperation(
                        new ChangeTrustOperation.Builder(ChangeTrustAsset.Create(_playMONEY)).Build(),
                        null);

                    airDropTx.AddOperation(new PaymentOperation.Builder(
                        KeyPair.FromAccountId(request.AccountId),
                        _playMONEY,
                        (totalTax * UsaPricedInMoney).ToString("N7"))
                        .SetSourceAccount(_airDropWallet.KeyPairWithSecretSeed)
                        .Build(),
                        _airDropWallet.KeyPairWithSecretSeed);

                    var airDropTxXdr = airDropTx.GetXdr();
                    var signedAirDropTxXdr = stellar_dotnet_sdk.Transaction.FromEnvelopeXdr(airDropTxXdr);
                    signedAirDropTxXdr.Sign(_airDropWallet.KeyPairWithSecretSeed, _airDropWallet.NetworkInfo);

                    return new TaxContributionResponse()
                    {
                        Success = new Success() { Success_ = true, Message = "Hello World!" },
                        SignedXdr = signedAirDropTxXdr.ToEnvelopeXdrBase64()
                    };
                }
            }
        }

        return new TaxContributionResponse() {  Success = new Success() {  Success_ = false } };
    }

    public override async Task<Success> SubmitAirDropTransaction(SignedTransaction request, ServerCallContext context)
    {
        await this.InitializeWalletsAsync();

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
}
