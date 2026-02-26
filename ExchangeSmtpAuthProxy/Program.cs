using Serilog;
using Serilog.Events;
using SmtpServer;
using SmtpServer.ComponentModel;
using SmtpServer.Tracing;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ExchangeSmtpAuthProxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
			// Allow full UTF8 output in console
			//Console.OutputEncoding = Encoding.UTF8;

			// Set console title to application name and version
			Assembly assembly = Assembly.GetExecutingAssembly();
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
			string title = versionInfo.ProductName + " " + versionInfo.ProductVersion;

			Console.Title = title;

			// Setup logging
			using var log = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.Enrich.WithProperty("Context", "Core")
				.WriteTo.File("logs/output.log", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({Context}) {Message:lj}{NewLine}{Exception}")
				.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] ({Context}) {Message:lj}{NewLine}{Exception}")
				.CreateLogger();
			Log.Logger = log;

			Log.Information("Starting {title}", title);

			Log.Information("Setting up SMTP server");
			var certificate = CreateCertificate();

			var builder = new SmtpServerOptionsBuilder()
				.ServerName("localhost");

			if (Config.Instance.AllowUnencrypted) {
				builder.Endpoint(builder =>
					builder
						.Port(25, false)
						.AllowUnsecureAuthentication(true)
						.AuthenticationRequired(Config.Instance.AuthenticationRequired));
			}

			if (Config.Instance.AllowUnencrypted)
			{
				builder.Endpoint(builder =>
					builder
						.Port(465, true)
						.AuthenticationRequired(Config.Instance.AuthenticationRequired)
						.Certificate(certificate));
			}

			if (Config.Instance.AllowUnencrypted)
			{
				builder.Endpoint(builder =>
					builder
						.Port(587, false)
						.AllowUnsecureAuthentication(false)
						.AuthenticationRequired(Config.Instance.AuthenticationRequired)
						.Certificate(certificate));
			}

			var options = builder.Build();

			if (options.Endpoints.Count == 0)
			{
				Log.Error("No endpoints configured. Please check your configuration.");
				return;
			}

			var serviceProvider = new ServiceProvider();
			serviceProvider.Add(new ForwardingMessageStore());
			serviceProvider.Add(new DomainUserAuthenticator());

			var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
			smtpServer.SessionCreated += SessionCreated;
			var serverTask = smtpServer.StartAsync(CancellationToken.None);

			Log.Information("SMTP server started. Listening on ports {ports}.", string.Join(", ", options.Endpoints.Select(e => e.Endpoint.Port)));

			await serverTask;

			Log.CloseAndFlush();
		}

		private static void SessionCreated(object? sender, SessionEventArgs e)
		{
			e.Context.Properties["Logger"] = Log.ForContext("Context", e.Context.SessionId);
		}

		private static X509Certificate CreateCertificate()
		{
			if (File.Exists(Config.Instance.CertificatePath))
			{
				return X509CertificateLoader.LoadPkcs12FromFile(Config.Instance.CertificatePath, Config.Instance.CertificatePassword);
			}

			using var rsa = RSA.Create(2048);

			var request = new CertificateRequest(
				new X500DistinguishedName("CN=localhost"),
				rsa,
				HashAlgorithmName.SHA256,
				RSASignaturePadding.Pkcs1
			);

			// Basic constraints, mark as end-entity certificate
			request.CertificateExtensions.Add(
				new X509BasicConstraintsExtension(false, false, 0, false));

			// Key usage
			request.CertificateExtensions.Add(
				new X509KeyUsageExtension(
					X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
					false));

			// Subject Alternative Name
			var sanBuilder = new SubjectAlternativeNameBuilder();
			sanBuilder.AddDnsName("localhost");
			sanBuilder.AddIpAddress(IPAddress.Loopback);
			request.CertificateExtensions.Add(sanBuilder.Build());

			// Self-sign the certificate
			var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
			var notAfter = DateTimeOffset.UtcNow.AddYears(1);

			X509Certificate2 cert = request.CreateSelfSigned(notBefore, notAfter);

			// Save the certificate for next time
			File.WriteAllBytes(Config.Instance.CertificatePath, cert.Export(X509ContentType.Pkcs12, Config.Instance.CertificatePassword));

			return cert;
		}
	}
}
