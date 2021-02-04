﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Smartstore.Collections;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Search.Modelling;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Stores;

namespace Smartstore.Core.Catalog.Products
{
    public partial class ProductUrlHelper
    {
        private readonly SmartDbContext _db;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly LinkGenerator _linkGenerator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<ICatalogSearchQueryAliasMapper> _catalogSearchQueryAliasMapper;
        private readonly Lazy<ILanguageService> _languageService;
        private readonly Lazy<LocalizationSettings> _localizationSettings;
        private readonly int _languageId;

        public ProductUrlHelper(
            SmartDbContext db,
            IWorkContext workContext,
            IStoreContext storeContext,
            LinkGenerator linkGenerator,
            IHttpContextAccessor httpContextAccessor,
            Lazy<ICatalogSearchQueryAliasMapper> catalogSearchQueryAliasMapper,
            Lazy<ILanguageService> languageService,
            Lazy<LocalizationSettings> localizationSettings)
        {
            _db = db;
            _workContext = workContext;
            _storeContext = storeContext;
            _linkGenerator = linkGenerator;
            _httpContextAccessor = httpContextAccessor;
            _catalogSearchQueryAliasMapper = catalogSearchQueryAliasMapper;
            _languageService = languageService;
            _localizationSettings = localizationSettings;

            _languageId = _workContext.WorkingLanguage.Id;
        }

        /// <summary>
        /// URL of the product page used to create the new product URL. Created from route if <c>null</c>.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Initial query string used to create the new query string. Usually <c>null</c>.
        /// </summary>
        public MutableQueryCollection InitialQuery { get; set; }

        /// <summary>
        /// Converts a query object into a URL query string.
        /// </summary>
        /// <param name="query">Product variant query.</param>
        /// <returns>URL query string.</returns>
        public virtual string ToQueryString(ProductVariantQuery query)
        {
            var qs = InitialQuery ?? new MutableQueryCollection();

            // Checkout attributes.
            foreach (var item in query.CheckoutAttributes)
            {
                if (item.Date.HasValue)
                {
                    qs.Add(item.ToString(), string.Join("-", item.Date.Value.Year, item.Date.Value.Month, item.Date.Value.Day));
                }
                else
                {
                    qs.Add(item.ToString(), item.Value);
                }
            }

            // Gift cards.
            foreach (var item in query.GiftCards)
            {
                qs.Add(item.ToString(), item.Value);
            }

            // Variants.
            foreach (var item in query.Variants)
            {
                if (item.Alias.IsEmpty())
                {
                    item.Alias = _catalogSearchQueryAliasMapper.Value.GetVariantAliasById(item.AttributeId, _languageId);
                }

                if (item.Date.HasValue)
                {
                    qs.Add(item.ToString(), string.Join("-", item.Date.Value.Year, item.Date.Value.Month, item.Date.Value.Day));
                }
                else if (item.IsFile || item.IsText)
                {
                    qs.Add(item.ToString(), item.Value);
                }
                else
                {
                    if (item.ValueAlias.IsEmpty())
                    {
                        item.ValueAlias = _catalogSearchQueryAliasMapper.Value.GetVariantOptionAliasById(item.Value.ToInt(), _languageId);
                    }

                    var value = item.ValueAlias.HasValue()
                        ? $"{item.ValueAlias}-{item.Value}"
                        : item.Value;

                    qs.Add(item.ToString(), value);
                }
            }

            return qs.ToString();
        }

        /// <summary>
        /// Deserializes an attributes selection into a product variant query.
        /// </summary>
        /// <param name="query">Product variant query.</param>
        /// <param name="productId">Product identifier.</param>
        /// <param name="selection">Selected attributes.</param>
        /// <param name="bundleItemId">Bundle item identifier.</param>
        /// <param name="attributes">Product variant attributes.</param>
        public virtual async Task DeserializeQueryAsync(
            ProductVariantQuery query,
            int productId,
            ProductVariantAttributeSelection selection,
            int bundleItemId = 0,
            ICollection<ProductVariantAttribute> attributes = null)
        {
            Guard.NotNull(query, nameof(query));

            if (productId == 0 || (selection?.AttributesMap?.Any() ?? false))
            {
                return;
            }

            if (attributes == null)
            {
                var ids = selection.AttributesMap.Select(x => x.Key);
                attributes = await _db.ProductVariantAttributes.GetManyAsync(ids);
            }

            foreach (var attribute in attributes)
            {
                var item = selection.AttributesMap.FirstOrDefault(x => x.Key == attribute.Id);
                if (item.Key != 0)
                {
                    foreach (var originalValue in item.Value)
                    {
                        var value = originalValue.ToString();
                        DateTime? date = null;

                        if (attribute.AttributeControlType == AttributeControlType.Datepicker)
                        {
                            date = value.ToDateTime(new[] { "D" }, CultureInfo.CurrentCulture, DateTimeStyles.None, null);
                            if (date == null)
                            {
                                continue;
                            }

                            value = string.Join("-", date.Value.Year, date.Value.Month, date.Value.Day);
                        }

                        var queryItem = new ProductVariantQueryItem(value)
                        {
                            ProductId = productId,
                            BundleItemId = bundleItemId,
                            AttributeId = attribute.ProductAttributeId,
                            VariantAttributeId = attribute.Id,
                            Alias = _catalogSearchQueryAliasMapper.Value.GetVariantAliasById(attribute.ProductAttributeId, _languageId),
                            Date = date,
                            IsFile = attribute.AttributeControlType == AttributeControlType.FileUpload,
                            IsText = attribute.AttributeControlType == AttributeControlType.TextBox || attribute.AttributeControlType == AttributeControlType.MultilineTextbox
                        };

                        if (attribute.IsListTypeAttribute())
                        {
                            queryItem.ValueAlias = _catalogSearchQueryAliasMapper.Value.GetVariantOptionAliasById(value.ToInt(), _languageId);
                        }

                        query.AddVariant(queryItem);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a product URL including variant query string.
        /// </summary>
        /// <param name="query">Product variant query.</param>
        /// <param name="productSlug">Product URL slug.</param>
        /// <returns>Product URL.</returns>
        public virtual string GetProductUrl(ProductVariantQuery query, string productSlug)
        {
            if (productSlug.IsEmpty())
            {
                return null;
            }

            var options = new LinkOptions { AppendTrailingSlash = false };
            var url = Url ?? _linkGenerator.GetUriByRouteValues(_httpContextAccessor.HttpContext, "Product", new { SeName = productSlug }, options: options);

            return url + ToQueryString(query);
        }

        /// <summary>
        /// Creates a product URL including variant query string.
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="productSlug">Product URL slug.</param>
        /// <param name="selection">Selected attributes.</param>
        /// <returns>Product URL.</returns>
        public virtual async Task<string> GetProductUrlAsync(int productId, string productSlug, ProductVariantAttributeSelection selection)
        {
            var query = new ProductVariantQuery();
            await DeserializeQueryAsync(query, productId, selection);

            return GetProductUrl(query, productSlug);
        }

        /// <summary>
        /// Creates an absolute product URL.
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <param name="productSlug">Product URL slug.</param>
        /// <param name="selection">Selected attributes.</param>
        /// <param name="store">Store.</param>
        /// <param name="language">Language.</param>
        /// <returns>Absolute product URL.</returns>
        public virtual async Task<string> GetAbsoluteProductUrlAsync(
            int productId,
            string productSlug,
            ProductVariantAttributeSelection selection,
            Store store = null,
            Language language = null)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            if (request == null || productSlug.IsEmpty())
            {
                return null;
            }

            var url = Url;

            if (url.IsEmpty())
            {
                store ??= _storeContext.CurrentStore;
                language ??= _workContext.WorkingLanguage;

                // No given URL. Create SEO friendly URL.
                var urlHelper = new LocalizedUrlHelper(request.PathBase.Value, productSlug);

                if (_localizationSettings.Value.SeoFriendlyUrlsForLanguagesEnabled)
                {
                    var defaultSeoCode = await _languageService.Value.GetMasterLanguageSeoCodeAsync(store.Id);

                    if (language.UniqueSeoCode == defaultSeoCode && _localizationSettings.Value.DefaultLanguageRedirectBehaviour > 0)
                    {
                        urlHelper.StripCultureCode();
                    }
                    else
                    {
                        urlHelper.PrependCultureCode(language.UniqueSeoCode, true);
                    }
                }

                var storeUrl = store.Url.TrimEnd('/');

                // Prevent duplicate occurrence of application path.
                if (urlHelper.PathBase.HasValue() && storeUrl.EndsWith(urlHelper.PathBase, StringComparison.OrdinalIgnoreCase))
                {
                    storeUrl = storeUrl.Substring(0, storeUrl.Length - urlHelper.PathBase.Length).TrimEnd('/');
                }

                url = storeUrl + urlHelper.FullPath;
            }

            if (selection?.AttributesMap?.Any() ?? false)
            {
                var query = new ProductVariantQuery();
                await DeserializeQueryAsync(query, productId, selection);

                url += ToQueryString(query);
            }

            return url;
        }
    }
}
