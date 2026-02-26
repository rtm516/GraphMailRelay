using Microsoft.Graph.Models;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExchangeSmtpAuthProxy
{
	public static class MimeMessageConverter
	{
		public static Message ToGraphMessage(MimeMessage mime)
		{
			var message = new Message
			{
				Subject = mime.Subject,
				Body = GetBody(mime),
				ToRecipients = ToRecipientList(mime.To),
				CcRecipients = ToRecipientList(mime.Cc),
				BccRecipients = ToRecipientList(mime.Bcc),
				ReplyTo = ToRecipientList(mime.ReplyTo),
				From = ToRecipient(mime.From.Mailboxes.FirstOrDefault()),
				Sender = ToRecipient(mime.Sender),
				Importance = ToImportance(mime.Priority),
				InternetMessageId = mime.MessageId,
			};

			if (mime.Date != DateTimeOffset.MinValue)
				message.SentDateTime = mime.Date;

			// Map custom/extra headers
			foreach (var header in mime.Headers)
			{
				message.AdditionalData ??= new Dictionary<string, object>();
				// Only carry through headers Graph doesn't natively handle
				if (!WellKnownHeaders.Contains(header.Field))
					message.AdditionalData[$"singleValueExtendedProperties_{header.Field}"] = header.Value;
			}

			message.Attachments = GetAttachments(mime);

			return message;
		}

		// -------------------------------------------------------------------------

		private static ItemBody GetBody(MimeMessage mime)
		{
			if (mime.HtmlBody is not null)
				return new ItemBody { ContentType = BodyType.Html, Content = mime.HtmlBody };

			return new ItemBody { ContentType = BodyType.Text, Content = mime.TextBody ?? string.Empty };
		}

		private static List<Recipient> ToRecipientList(InternetAddressList addresses)
		{
			return addresses
				.Mailboxes
				.Select(ToRecipient)
				.Where(r => r is not null)
				.ToList()!;
		}

		private static Recipient? ToRecipient(MailboxAddress? mailbox)
		{
			if (mailbox is null) return null;

			return new Recipient
			{
				EmailAddress = new EmailAddress
				{
					Address = mailbox.Address,
					Name = mailbox.Name
				}
			};
		}

		private static Importance? ToImportance(MessagePriority priority) => priority switch
		{
			MessagePriority.Urgent => Importance.High,
			MessagePriority.NonUrgent => Importance.Low,
			_ => Importance.Normal
		};

		private static List<Attachment> GetAttachments(MimeMessage mime)
		{
			var attachments = new List<Attachment>();

			foreach (var part in mime.BodyParts)
			{
				// Skip the parts that form the main body
				if (part is TextPart textPart && !textPart.IsAttachment)
					continue;

				if (part is MimePart mimePart)
				{
					using var ms = new MemoryStream();
					mimePart.Content.DecodeTo(ms);

					attachments.Add(new FileAttachment
					{
						OdataType = "#microsoft.graph.fileAttachment",
						Name = mimePart.FileName ?? mimePart.ContentId ?? "attachment",
						ContentType = mimePart.ContentType.MimeType,
						ContentBytes = ms.ToArray(),
						IsInline = mimePart.IsAttachment == false,
						ContentId = mimePart.ContentId
					});
				}
			}

			return attachments;
		}

		private static readonly HashSet<string> WellKnownHeaders = new(StringComparer.OrdinalIgnoreCase)
		{
			"From", "To", "Cc", "Bcc", "Subject", "Date", "Reply-To",
			"Sender", "Message-Id", "In-Reply-To", "References",
			"Content-Type", "Content-Transfer-Encoding", "MIME-Version",
			"X-Priority", "Importance"
		};
	}
}
