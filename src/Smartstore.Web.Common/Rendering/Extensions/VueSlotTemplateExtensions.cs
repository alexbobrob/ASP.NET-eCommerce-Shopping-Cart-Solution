﻿using System;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Smartstore.Core.Localization;

namespace Smartstore.Web.Rendering
{
    public static class VueSlotTemplateExtensions
    {
        // TODO: (mg) (core) Port other helpers: LabeledCurrencyName

        /// <summary>
        /// Renders labeled product for grids including link to the edit page. Not intended to be used outside of grids (use BadgedProductName there).
        /// </summary>
        public static IHtmlContent LabeledProductName(
            this IHtmlHelper _,
            string typeNameExpression = "item.row.ProductTypeName",
            string typeLabelHintExpression = "item.row.ProductTypeLabelHint",
            string urlExpression = "item.row.EditUrl",
            string valueExpression = "item.value",
            string linkTarget = null)
        {
            var builder = new HtmlContentBuilder();

            if (typeNameExpression.HasValue() && typeLabelHintExpression.HasValue())
            {
                var label = "<span class='mr-1 badge' :class=\"'badge-' + {0}\">{{{{ {1} }}}}</span>".FormatInvariant(typeLabelHintExpression, typeNameExpression);
                builder.AppendHtml(label);
            }

            var isLink = urlExpression.HasValue();
            var name = new TagBuilder(isLink ? "a" : "span");
            name.Attributes.Add("class", "text-truncate");
            name.InnerHtml.AppendHtml("{{ " + valueExpression + " }}");

            if (isLink)
            {
                name.Attributes.Add("v-bind:href", urlExpression);
                if (linkTarget.HasValue())
                {
                    name.Attributes.Add("target", linkTarget);
                }
            }

            builder.AppendHtml(name);

            return builder;
        }

        /// <summary>
        /// Renders a labeled order number for grids including a link to the edit page. Not intended to be used outside of grids.
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="hasNewPaymentNotificationExpression">Row expression of HasNewPaymentNotification property.</param>
        /// <param name="urlExpression">Row expression of the edit page URL.</param>
        /// <param name="linkTarget">Value of the HTML target attribute.</param>
        /// <returns>Labeled order number.</returns>
        public static IHtmlContent LabeledOrderNumber(
            this IHtmlHelper helper,
            string hasNewPaymentNotificationExpression = "item.row.HasNewPaymentNotification",
            string urlExpression = "item.row.EditUrl",
            string linkTarget = null)
        {
            var builder = new HtmlContentBuilder();

            if (hasNewPaymentNotificationExpression.HasValue())
            {
                var localizationService = helper.ViewContext.HttpContext.RequestServices.GetService<ILocalizationService>();

                var label = new TagBuilder("span");
                label.Attributes.Add("v-if", hasNewPaymentNotificationExpression);
                label.Attributes.Add("class", "badge badge-warning mr-1");
                label.Attributes.Add("title", localizationService.GetResource("Admin.Orders.Payments.NewIpn.Hint"));
                label.InnerHtml.Append(localizationService.GetResource("Admin.Orders.Payments.NewIpn"));

                builder.AppendHtml(label);
            }

            var isLink = urlExpression.HasValue();
            var name = new TagBuilder(isLink ? "a" : "span");
            name.Attributes.Add("class", "text-truncate");
            name.InnerHtml.AppendHtml("{{ item.value }}");

            if (isLink)
            {
                name.Attributes.Add("v-bind:href", urlExpression);
                if (linkTarget.HasValue())
                {
                    name.Attributes.Add("target", linkTarget);
                }
            }

            builder.AppendHtml(name);

            return builder;
        }

        /// <summary>
        /// Renders a labeled variant attribute value name including link icon and color square for grids. Not intended to be used outside of grids.
        /// </summary>
        /// <returns>Labeled variant attribute value name</returns>
        public static IHtmlContent VariantAttributeValueName(this IHtmlHelper _)
        {
            var builder = new HtmlContentBuilder();

            var icon = "<i :class='item.row.TypeNameClass' :title='item.row.TypeName'></i>";
            builder.AppendHtml(icon);

            var colorSpan = new TagBuilder("span");
            colorSpan.Attributes.Add("v-if", "item.row.HasColor");
            colorSpan.Attributes.Add("class", "color-container");
            colorSpan.InnerHtml.AppendHtml("<span class='color' :style='{ background: item.row.Color }'>&nbsp;</span>");
            builder.AppendHtml(colorSpan);

            var quantityInfo = "<span>{{ item.value }} {{ item.row.QuantityInfo }}</span>";
            builder.AppendHtml(quantityInfo);

            return builder;
        }

        /// <summary>
        /// Renders a specification attribute option name (including color square) for grids. Not intended to be used outside of grids.
        /// </summary>
        /// <returns>Specification attribute option name.</returns>
        public static IHtmlContent SpecificationAttributeOptionName(this IHtmlHelper _)
        {
            var colorSpan = new TagBuilder("span");
            colorSpan.Attributes.Add("v-if", "item.row.Color?.length > 0");
            colorSpan.Attributes.Add("class", "color-container");
            colorSpan.InnerHtml.AppendHtml("<span class='color' :style='{ background: item.row.Color }' :title='item.row.Color'>&nbsp;</span>");

            var builder = new HtmlContentBuilder();
            builder.AppendHtml(colorSpan);
            builder.AppendHtml("<a href='javascript:void(0)' class='edit-specification-attribute-option' :data-id='item.row.Id'>{{ item.value }}</a>");

            return builder;
        }
    }
}
