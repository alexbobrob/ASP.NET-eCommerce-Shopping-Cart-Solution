﻿//using System.ComponentModel.DataAnnotations;
//using System.ComponentModel.DataAnnotations.Schema;
//using System.Diagnostics.CodeAnalysis;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Metadata.Builders;
//using Smartstore.Core.Customers;

//namespace Smartstore.Core.Content.Blogs
//{
//    public class BlogCommentMap : IEntityTypeConfiguration<BlogComment>
//    {
//        public void Configure(EntityTypeBuilder<BlogComment> builder)
//        {
//            builder.HasOne(c => c.BlogPost)
//                .WithMany()
//                .HasForeignKey(c => c.BlogPostId);
//        }
//    }

//    /// <summary>
//    /// Represents a blog comment.
//    /// </summary>
//    [Table("BlogComment")] // Enables EF TPT inheritance
//    public partial class BlogComment : CustomerContent
//    {
//        private readonly ILazyLoader _lazyLoader;

//        public BlogComment()
//        {
//        }

//        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private member.", Justification = "Required for EF lazy loading")]
//        private BlogComment(ILazyLoader lazyLoader)
//        {
//            _lazyLoader = lazyLoader;
//        }

//        /// <summary>
//        /// Gets or sets the comment text.
//        /// </summary>
//        [MaxLength]
//        public string CommentText { get; set; }

//        /// <summary>
//        /// Gets or sets the blog post identifier.
//        /// </summary>
//        public int BlogPostId { get; set; }

//        private BlogPost _blogPost;
//        /// <summary>
//        /// Gets or sets the blog post.
//        /// </summary>
//        public virtual BlogPost BlogPost {
//            get => _lazyLoader?.Load(this, ref _blogPost) ?? _blogPost;
//            set => _blogPost = value;
//        }
//    }
//}
