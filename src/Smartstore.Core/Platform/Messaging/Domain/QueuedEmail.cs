﻿using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartstore.Domain;

namespace Smartstore.Core.Messages
{
    public class QueuedEmailMap : IEntityTypeConfiguration<QueuedEmail>
    {
        public void Configure(EntityTypeBuilder<QueuedEmail> builder)
        {
            builder.HasOne(x => x.EmailAccount)
                .WithMany()
                .HasForeignKey(x => x.EmailAccountId);
        }
    }

    /// <summary>
    /// Represents a queued email item.
    /// </summary>
    [Index(nameof(CreatedOnUtc), Name = "[IX_QueuedEmail_CreatedOnUtc]")]
    [Index(nameof(EmailAccountId), Name = "IX_EmailAccountId")]
    public partial class QueuedEmail : BaseEntity
    {
        private readonly ILazyLoader _lazyLoader;

        public QueuedEmail()
        {
        }

        public QueuedEmail(ILazyLoader lazyLoader)
        {
            _lazyLoader = lazyLoader;
        }

        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the From property.
        /// </summary>
        public string From { get; set; }

        /// <summary>
        /// Gets or sets the To property.
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// Gets or sets the ReplyTo property.
        /// </summary>
        public string ReplyTo { get; set; }

        /// <summary>
        /// Gets or sets the CC.
        /// </summary>
        public string CC { get; set; }

        /// <summary>
        /// Gets or sets the Bcc.
        /// </summary>
        public string Bcc { get; set; }

        /// <summary>
        /// Gets or sets the subject.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the body.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Gets or sets the date and time of item creation in UTC.
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the send tries.
        /// </summary>
        public int SentTries { get; set; }

        /// <summary>
        /// Gets or sets the sent date and time.
        /// </summary>
        public DateTime? SentOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the used email account identifier.
        /// </summary>
        public int EmailAccountId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether emails are only send manually.
        /// </summary>
        public bool SendManually { get; set; }

        private EmailAccount _emailAccount;
        /// <summary>
        /// Gets the email account.
        /// </summary>
        public EmailAccount EmailAccount
        {
            get => _lazyLoader?.Load(this, ref _emailAccount) ?? _emailAccount;
            set => _emailAccount = value;
        }

        private ICollection<QueuedEmailAttachment> _attachments;
        /// <summary>
        /// Gets or sets the collection of attachments.
        /// </summary>
        public ICollection<QueuedEmailAttachment> Attachments
        {
            get => _lazyLoader?.Load(this, ref _attachments) ?? (_attachments ??= new HashSet<QueuedEmailAttachment>());
            protected set => _attachments = value;
        }
    }
}
