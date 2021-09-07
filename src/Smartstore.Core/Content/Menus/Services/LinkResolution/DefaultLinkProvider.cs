﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Catalog.Brands;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Content.Topics;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Widgets;
using Smartstore.Domain;

namespace Smartstore.Core.Content.Menus
{
    public partial class LinkBuilderMetadata
    {
        public int Order { get; init; }
        public string Schema { get; init; }
        public string Icon { get; init; }
        public string ResKey { get; init; }
        public WidgetInvoker Widget { get; init; }
    }

    public partial interface ILinkProvider
    {
        int Order { get; }
        Task<LinkTranslationResult> TranslateAsync(LinkExpression expression, int storeId, int languageId);
        IEnumerable<LinkBuilderMetadata> GetBuilderMetadata();
    }

    public partial class DefaultLinkProvider : ILinkProvider
    {
        public const string SchemaTopic = "topic";
        public const string SchemaProduct = "product";
        public const string SchemaCategory = "category";
        public const string SchemaManufacturer = "manufacturer";
        public const string SchemaUrl = "url";
        public const string SchemaFile = "file";

        private static string[] _supportedSchemas =
            new[] { SchemaTopic, SchemaProduct, SchemaCategory, SchemaManufacturer, SchemaUrl, SchemaFile };

        private readonly SmartDbContext _db;
        private readonly IUrlHelper _urlHelper;

        public DefaultLinkProvider(SmartDbContext db, IUrlHelper urlHelper)
        {
            _db = db;
            _urlHelper = urlHelper;
        }

        public int Order { get; }

        public virtual IEnumerable<LinkBuilderMetadata> GetBuilderMetadata()
        {
            return new[]
            {
                new LinkBuilderMetadata { Schema = SchemaProduct, Icon = "fa fa-cube", ResKey = "Common.Entity.Product" },
                new LinkBuilderMetadata { Schema = SchemaCategory, Icon = "fa fa-sitemap", ResKey = "Common.Entity.Category" },
                new LinkBuilderMetadata { Schema = SchemaManufacturer, Icon = "far fa-building", ResKey = "Common.Entity.Manufacturer" },
                new LinkBuilderMetadata { Schema = SchemaTopic, Icon = "far fa-file-alt", ResKey = "Common.Entity.Topic" },
                new LinkBuilderMetadata { Schema = SchemaFile, Icon = "far fa-folder-open", ResKey = "Common.File", Order = 100 },
                new LinkBuilderMetadata { Schema = SchemaUrl, Icon = "fa fa-link", ResKey = "Common.Url", Order = 200 }
            };
        }

        public virtual async Task<LinkTranslationResult> TranslateAsync(LinkExpression expression, int storeId, int languageId)
        {
            if (!_supportedSchemas.Contains(expression.Schema))
            {
                return null;
            }

            Type entityType = null;
            string entityName = null;
            LinkTranslatorEntitySummary summary = null;

            switch (expression.Schema)
            {
                case SchemaUrl:
                    var url = expression.TargetAndQuery;
                    if (url.StartsWith('~'))
                    {
                        url = _urlHelper.Content(url);
                    }

                    return new LinkTranslationResult { Link = url };
                case SchemaFile:
                    // TODO: (core) Really LinkStatus.Ok here without any further checks?
                    return new LinkTranslationResult { Link = expression.Target };
                case SchemaTopic:
                    entityType = typeof(Topic);
                    entityName = nameof(Topic);
                    summary = await GetEntityDataAsync<Topic>(expression, storeId, languageId, x => null);
                    break;
                case SchemaProduct:
                    entityType = typeof(Product);
                    entityName = nameof(Product);
                    summary = await GetEntityDataAsync<Product>(expression, storeId, languageId, x => new LinkTranslatorEntitySummary
                    {
                        Name = x.Name,
                        Published = x.Published,
                        Deleted = x.Deleted,
                        SubjectToAcl = x.SubjectToAcl,
                        LimitedToStores = x.LimitedToStores,
                        PictureId = x.MainPictureId
                    });
                    break;
                case SchemaCategory:
                    entityType = typeof(Category);
                    entityName = nameof(Category);
                    summary = await GetEntityDataAsync<Category>(expression, storeId, languageId, x => new LinkTranslatorEntitySummary
                    {
                        Name = x.Name,
                        Published = x.Published,
                        Deleted = x.Deleted,
                        SubjectToAcl = x.SubjectToAcl,
                        LimitedToStores = x.LimitedToStores,
                        PictureId = x.MediaFileId
                    });
                    break;
                case SchemaManufacturer:
                    entityType = typeof(Manufacturer);
                    entityName = nameof(Manufacturer);
                    summary = await GetEntityDataAsync<Manufacturer>(expression, storeId, languageId, x => new LinkTranslatorEntitySummary
                    {
                        Name = x.Name,
                        Published = x.Published,
                        Deleted = x.Deleted,
                        SubjectToAcl = x.SubjectToAcl,
                        LimitedToStores = x.LimitedToStores,
                        PictureId = x.MediaFileId
                    });
                    break;
            }

            return new LinkTranslationResult
            { 
                EntitySummary = summary,
                EntityName = entityName,
                EntityType = entityType,
                Status = summary == null || summary.Deleted 
                    ? LinkStatus.NotFound 
                    : summary.Published ? LinkStatus.Ok : LinkStatus.Hidden
            };
        }

        private async Task<LinkTranslatorEntitySummary> GetEntityDataAsync<T>(LinkExpression expression,
            int storeId,
            int languageId,
            Expression<Func<T, LinkTranslatorEntitySummary>> selector) where T : BaseEntity
        {
            LinkTranslatorEntitySummary summary = null;
            string systemName = null;

            if (!int.TryParse(expression.Target, out var entityId))
            {
                systemName = expression.Target;
            }

            if (expression.Schema == "topic")
            {
                Topic topic = null;

                if (string.IsNullOrEmpty(systemName))
                {
                    topic = await _db.Topics.FindByIdAsync(entityId, false);
                }
                else
                {
                    topic = await _db.Topics
                        .AsNoTracking()
                        .ApplyStandardFilter(true, null, storeId)
                        .FirstOrDefaultAsync(x => x.SystemName == systemName);
                }

                if (topic != null)
                {
                    summary = new LinkTranslatorEntitySummary
                    {
                        Id = topic.Id,
                        Name = topic.SystemName,
                        Title = topic.Title,
                        ShortTitle = topic.ShortTitle,
                        Published = topic.IsPublished,
                        SubjectToAcl = topic.SubjectToAcl,
                        LimitedToStores = topic.LimitedToStores,
                        LocalizedPropertyNames = new[] { nameof(Topic.ShortTitle), nameof(Topic.Title) }
                    };
                }
            }
            else
            {
                summary = await _db.Set<T>()
                    .AsNoTracking()
                    .Where(x => x.Id == entityId)
                    .Select(selector)
                    .SingleOrDefaultAsync();

                if (summary != null)
                {
                    summary.Id = entityId;
                    summary.LocalizedPropertyNames = new[] { "Name" };
                }
            }

            return summary;
        }
    }
}
