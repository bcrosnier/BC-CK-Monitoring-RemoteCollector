using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Monitoring.ReceiverFunctionApp
{
    public static class Constants
    {
        public const string CkmonProcessQueueName = "ckmonprocessor";
        public const string AzureStorageAccountName = "AzureWebJobsCkmon";
        public const string AzureBlobContainerName = "ckmon";
        public const string AzureFunctionsKeyIdClaimType = "http://schemas.microsoft.com/2017/07/functions/claims/keyid";
    }
}
