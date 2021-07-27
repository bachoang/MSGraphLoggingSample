using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSGraphLoggingSample
{
    class Program
    {
        static AppendBlobClient blobclient = null;

        static string ClientId = AzureSettings.ClientId;
        static string TenantId = AzureSettings.TenantId;
        static string MSALAzureBlobContainerName = AzureSettings.MSGraphAzureBlobContainerName;
        static string MSALAzureBlobName = AzureSettings.MSALAzureBlobName;
        static string MSGraphAzureBlobContainerName = AzureSettings.MSGraphAzureBlobContainerName;
        static string MSGraphAzureBlobName = AzureSettings.MSGraphAzureBlobName;
        static string LoggingLocalPath = AzureSettings.LoggingLocalPath;
        static string AzureStorageConnection = AzureSettings.AzureStorageConnection;
        static string[] Scopes = AzureSettings.Scopes;

        static AzureConfig _config = null;
        public static AzureConfig AzureSettings
        {
            get
            {
                // only load this once when the app starts.
                // To reload, you will have to set the variable _config to null again before calling this property
                if (_config == null)
                {
                    _config = new AzureConfig();
                    IConfiguration builder = new ConfigurationBuilder()
                        .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .Build();

                    ConfigurationBinder.Bind(builder.GetSection("Azure"), _config);
                }

                return _config;
            }
        }

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                // Serilog console logging
                .WriteTo.Console()
                // Serilog file logging
                .WriteTo.File(LoggingLocalPath, rollingInterval: RollingInterval.Day)
                // Serilog Azure Blob Storage logging
                .WriteTo.AzureBlobStorage(AzureStorageConnection, Serilog.Events.LogEventLevel.Verbose, MSGraphAzureBlobContainerName, MSGraphAzureBlobName)
                .CreateLogger();

            IPublicClientApplication publicClientApplication = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithTenantId(TenantId)
                // Enable MSAL logging
                .WithLogging(MSALlogger, Microsoft.Identity.Client.LogLevel.Verbose, true)
                .WithRedirectUri("http://localhost")
                .Build();

            InteractiveAuthenticationProvider authProvider = new InteractiveAuthenticationProvider(publicClientApplication, Scopes);

            // get the default list of handlers and add the logging handler to the list
            var handlers = GraphClientFactory.CreateDefaultHandlers(authProvider);

            // Remove Compression handler
            var compressionHandler =
                handlers.Where(h => h is CompressionHandler).FirstOrDefault();
            handlers.Remove(compressionHandler);

            // Add SeriLog logger
            handlers.Add(new SeriLoggingHandler());

            InitializeBlobStorageForMSAL().Wait();

            var httpClient = GraphClientFactory.Create(handlers);
            GraphServiceClient graphClient = new GraphServiceClient(httpClient);

            Get_Me(graphClient).Wait();
            CreateApplication(graphClient).Wait();

            Log.CloseAndFlush();
        }

        static async Task Get_Me(GraphServiceClient graphClient)
        {
            try
            {
                User me = await graphClient.Me.Request().GetAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Error calling Get_Me method");
                Log.Error(ex, "Exception from MS Graph call");
            }
        }

        static async Task CreateApplication(GraphServiceClient graphClient)
        {
            try
            {
                PermissionScope scope = new PermissionScope
                {
                    Value = "user_impersionation",
                    Id = System.Guid.NewGuid(),
                    Type = "User",
                    AdminConsentDisplayName = "display name",
                    UserConsentDescription = "display name",
                    // UserConsentDisplayName = "xxx",
                    // IsEnabled = true,
                    AdminConsentDescription = "xxx"
                };

                IEnumerable<PermissionScope> items = new PermissionScope[] { scope };
                var api2 = new ApiApplication
                {
                    Oauth2PermissionScopes = items,
                };

                IEnumerable<string> appiduri = new string[] { "https://localhost:8080/api4" };
                var webapi = new Application
                {
                    DisplayName = "POC_WebApi4",
                    SignInAudience = "AzureADMyOrg",
                    Api = api2,
                    IdentifierUris = appiduri,

                };
                Application app = await graphClient.Applications
                    .Request()
                    .AddAsync(webapi);
            }
            catch (Exception ex)
            {
                Log.Error("Error calling CreateApplication method");
                Log.Error(ex, "Exception from MS Graph call");
            }
        }

        static async Task InitializeBlobStorageForMSAL()
        {

            try
            {
                BlobContainerClient container = new BlobContainerClient(AzureStorageConnection, MSALAzureBlobContainerName);
                // container.
                // await container.G.CreateAsync();
                // Create the container if it doesn't already exist.  
                await container.CreateIfNotExistsAsync();
                blobclient = container.GetAppendBlobClient($"{DateTime.Today.ToString("MM-dd-yyyy")}-{MSALAzureBlobName}");
                // Create Azure Blob if it does not exist
                await blobclient.CreateIfNotExistsAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("error initializing Azure Blob Storage");
                Console.WriteLine(e.ToString());
            }
        }

        private static void MSALlogger(Microsoft.Identity.Client.LogLevel level, string message, bool containsPii)
        {
            string blobContents = $"{DateTime.UtcNow}: {level} {message}\n";
            byte[] byteArray = Encoding.ASCII.GetBytes(blobContents);

            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                try
                {
                    blobclient.AppendBlock(stream);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to write MSAL logging to Azure Storage Blob");
                    Console.WriteLine(e.ToString());
                }
            }
        }

        static string GetRequestHeader(HttpRequestMessage request)
        {
            Log.Information("Request Header:");
            return request.ToString();
        }
        static string GetRequestBody(HttpRequestMessage request)
        {
            Log.Information("Request Body:");
            string requestbody = "no content";
            if (request.Content != null)
            {
                requestbody = request.Content.ReadAsStringAsync().Result;
            }

            return requestbody;

        }

        static string GetResponseHeader(HttpResponseMessage response)
        {
            Log.Information("Respone Header:");
            return response.ToString();
        }

        static string GetResponseBody(HttpResponseMessage response)
        {
            Log.Information("Response Body:");
            string responsebody = "no content";
            if (response.Content != null)
            {
                responsebody = response.Content.ReadAsStringAsync().Result;
            }
            return responsebody;
        }

        public class SeriLoggingHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequest, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = null;

                try
                {
                    Log.Information("sending Graph Request");
                    Log.Debug(GetRequestHeader(httpRequest));
                    Log.Debug(GetRequestBody(httpRequest));
                    response = await base.SendAsync(httpRequest, cancellationToken);
                    Log.Information("Receiving Response:");
                    Log.Debug(GetResponseHeader(response));
                    Log.Debug(GetResponseBody(response));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Something went wrong");
                    if (response.Content != null)
                    {
                        await response.Content.ReadAsByteArrayAsync();// Drain response content to free connections.
                    }
                }
                return response;
            }
        }
    }
}
