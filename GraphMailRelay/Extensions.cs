using Serilog;
using SmtpServer;
using System;
using System.Collections.Generic;
using System.Text;

namespace GraphMailRelay
{
	public static class Extensions
	{
		extension(ISessionContext context)
		{
			public ILogger Logger => context.Properties["Logger"] as ILogger ?? Log.Logger;
		}
	}
}
