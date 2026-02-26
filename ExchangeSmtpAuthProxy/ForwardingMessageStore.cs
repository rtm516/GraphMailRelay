using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Identity.Client;
using MimeKit;
using Serilog;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Net;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Buffers;
using System.Net;

namespace ExchangeSmtpAuthProxy
{
	internal class ForwardingMessageStore : IMessageStore
	{
		public async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
		{
			await using var stream = new MemoryStream();

			var position = buffer.GetPosition(0);
			while (buffer.TryGet(ref position, out var memory))
			{
				stream.Write(memory.Span);
			}

			stream.Position = 0;

			var message = await MimeMessage.LoadAsync(stream, cancellationToken);

			var client = new GraphServiceClient(new ClientSecretCredential(Config.Instance.TenantId, Config.Instance.ClientId, Config.Instance.ClientSecret));

			var mail = new SendMailPostRequestBody
			{
				Message = MimeMessageConverter.ToGraphMessage(message),
				SaveToSentItems = true
			};

			// If we dont have a from address then use the authenticated address if its there
			if (context.Authentication.IsAuthenticated && (mail.Message.From == null || string.IsNullOrEmpty(mail.Message.From.EmailAddress.Address)))
			{
				mail.Message.From = new Recipient
				{
					EmailAddress = new EmailAddress
					{
						Address = context.Authentication.User
					}
				};
			}

			var emailAddress = context.Authentication.IsAuthenticated ? context.Authentication.User : message.From[0].ToString();

			try
			{
				await client.Users[emailAddress].SendMail.PostAsync(mail);

				context.Logger.Information("Request to send email to {to} from {from} with subject {subject}", message.To.ToString(), message.From.ToString(), message.Subject);
			}
			catch (Exception e)
			{
				return new SmtpResponse(SmtpReplyCode.TransactionFailed, e.Message);
			}

			return SmtpResponse.Ok;
		}
	}
}