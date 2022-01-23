using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RivalCoins.Sdk
{
    public static class ExtensionMethods
    {
        public static string Code(this Asset asset)
        {
            return asset.CanonicalName().Split(':')[0];
        }

        public static string Issuer(this Asset asset)
        {
            return asset.CanonicalName().Split(':')[1];
        }
    }
}
