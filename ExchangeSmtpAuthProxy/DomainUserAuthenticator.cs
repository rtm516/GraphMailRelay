using Microsoft.Identity.Client;
using SmtpServer;
using SmtpServer.Authentication;
using System.DirectoryServices.AccountManagement;

namespace ExchangeSmtpAuthProxy
{
	internal class DomainUserAuthenticator : IUserAuthenticator
	{
		class InlineHttpClientFactory : IMsalHttpClientFactory
		{
			private readonly HttpClient _client;
			public InlineHttpClientFactory() => _client = new HttpClient(new HttpClientHandler
			{
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
			});
			public HttpClient GetHttpClient() => _client;
		}

		public async Task<bool> AuthenticateAsync(ISessionContext context, string username, string password, CancellationToken cancellationToken)
		{
			// If authentication is not required, allow all users
			if (!Config.Instance.AuthenticationRequired) return true;

			context.Logger.Information("Authenticating user {username} from {client}", username, context.Properties["EndpointListener:RemoteEndPoint"]);

			var valid = false;
			var method = "";

			// TODO Replace with non-windows compatable method
			using (PrincipalContext principalContext = new PrincipalContext(ContextType.Domain))
			{
				context.Logger.Information(principalContext.ConnectedServer);
				//valid = principalContext.ValidateCredentials(username, password);
				if (valid) method = "AD";
			}

			// If AD auth failed then try Azure
			if (!valid)
			{
				var app = ConfidentialClientApplicationBuilder
					.Create(Config.Instance.ClientId)
					.WithClientSecret(Config.Instance.ClientSecret)
					.WithTenantId(Config.Instance.TenantId)
					.WithAuthority(AzureCloudInstance.AzurePublic, Config.Instance.TenantId)
					.WithHttpClientFactory(new InlineHttpClientFactory())
					.Build();

				try
				{
					// Not sure why we have to cast this to one of the types it supports but it does now work
					IByUsernameAndPassword ropcClient = (IByUsernameAndPassword)app;

#pragma warning disable CS0618 // Marked as obsolete in due to it not being supported outside of desktop apps but we are a desktop app
					var result = await ropcClient
						.AcquireTokenByUsernamePassword(new[] { "https://graph.microsoft.com/.default" }, username, password)
						.ExecuteAsync();
#pragma warning restore CS0618

					method = "Azure";
					valid = true;
				}
				catch (Exception ex)
				{
					context.Logger.Error("Error occured while authenticating user {username} with Azure: {error}", username, ex.Message);
				}
			}

			// Log the result of the authentication attempt
			if (valid)
			{
				context.Logger.Information("Successfully authenticated user {username} using {method}", username, method);
			}
			else
			{
				context.Logger.Warning("Failed to authenticate user {username} using both AD and Azure", username);
			}

			return valid;
		}
	}
}