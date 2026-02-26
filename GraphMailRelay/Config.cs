using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace GraphMailRelay
{
	internal class Config
	{
		public bool AuthenticationRequired { get; set; } = true;
		public string TenantId { get; set; } = string.Empty;
		public string ClientId { get; set; } = string.Empty;
		public string ClientSecret { get; set; } = string.Empty;
		public string CertificatePath { get; set; } = string.Empty;
		public string CertificatePassword { get; set; } = string.Empty;
		public bool AllowUnencrypted { get; set; } = false;
		public bool AllowSsl { get; set; } = false;
		public bool AllowStartTls { get; set; } = true;

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
