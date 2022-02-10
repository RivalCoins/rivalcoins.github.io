using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;

namespace RivalCoins.Sdk
{
    public class Transaction
    {
        private string _sourceAccountSeed;
        private AccountResponse _account;

        private Transaction()
        {
        }

        public static async Task<Transaction> CreateAsync(KeyPair transactionSourceAccount, Server server)
        {
            var transaction = new Transaction();

            transaction._sourceAccountSeed = transactionSourceAccount.CanSign() ? transactionSourceAccount.SecretSeed : null;
            transaction._account = await server.Accounts.Account(transactionSourceAccount.AccountId);
            transaction.Builder = new TransactionBuilder(transaction._account);

            return transaction;
        }

        private TransactionBuilder Builder { get; set; }
        private List<KeyPair> SourceAccounts { get; } = new List<KeyPair>();

        public void AddOperation(Operation operation, KeyPair operationSourceAccount)
        {
            Builder.AddOperation(operation);

            if (operationSourceAccount != null)
            {
                SourceAccounts.Add(operationSourceAccount);
            }
        }

        public string GetXdr()
        {
            var transaction = Builder.Build();

            return transaction.ToUnsignedEnvelopeXdrBase64();
        }

        public async Task<bool> SubmitAsync(Server server, stellar_dotnet_sdk.Network network, string logDescription)
        {
            try
            {
                var transaction = Builder.Build();

                SourceAccounts.Add(KeyPair.FromSecretSeed(_sourceAccountSeed));

                foreach (var sourceAccountSeed in SourceAccounts.Select(account => account.SecretSeed).Distinct())
                {
                    transaction.Sign(KeyPair.FromSecretSeed(sourceAccountSeed), network);
                }

                var response = await server.SubmitTransaction(transaction);
                Console.WriteLine($"{logDescription} - Success: {response.IsSuccess()}");

                if (!response.IsSuccess())
                {
                    Console.WriteLine($"{logDescription} - Failure Message: {response.ResultXdr}");
                }

                return response.IsSuccess();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{logDescription} Exception: {exception.Message + Environment.NewLine + exception.StackTrace}");
            }
            finally
            {
                SourceAccounts.Clear();
                SourceAccounts.Add(KeyPair.FromSecretSeed(_sourceAccountSeed));

                this.Builder = new TransactionBuilder(_account);
            }

            return false;
        }
    }
}
