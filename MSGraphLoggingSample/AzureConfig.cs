using System;
using System.Collections.Generic;
using System.Text;

namespace MSGraphLoggingSample
{
    class AzureConfig
    {
        public string ClientId { get; set; }
        public string TenantId { get; set; }
        public string LoggingLocalPath { get; set; }
        public string AzureStorageConnection { get; set; }
        public string MSALAzureBlobContainerName { get; set; }
        public string MSALAzureBlobName { get; set; }
        public string MSGraphAzureBlobContainerName { get; set; }
        public string MSGraphAzureBlobName { get; set; }
        public string[] Scopes { get; set; }
    }
}
