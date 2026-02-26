using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ExchangeSmtpAuthProxy
{
	internal class Config
	{
		public bool AuthenticationRequired { get; set; }
		public string TenantId { get; set; }
		public string ClientId { get; set; }
		public string ClientSecret { get; set; }
		public string CertificatePath { get; set; }
		public string CertificatePassword { get; set; }
		public bool AllowUnencrypted { get; set; }
		public bool AllowSsl { get; set; }
		public bool AllowStartTls { get; set; }

		public static Config Instance { get; } = Build();

		private static Config Build()
		{
			var builder = new ConfigurationBuilder()
				.AddJsonFile("appsettings.json")
				.AddJsonFile("appsettings.local.json", true)
				.Build();

			var config = builder.Get<Config>();

			if (config == null)
			{
				throw new InvalidOperationException("Failed to load configuration.");
			}

			return config;
		}
	}
}
