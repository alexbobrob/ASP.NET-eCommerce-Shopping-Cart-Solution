﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.XSS;
using AngleSharp.Css.Dom;

namespace Smartstore.Utilities.Html
{
    /// <summary>
    /// Utility class for html manipulation or creation
    /// </summary>
    public static partial class HtmlUtils
    {
        private readonly static Regex _paragraphStartRegex = new("<p>", RegexOptions.IgnoreCase);
        private readonly static Regex _paragraphEndRegex = new("</p>", RegexOptions.IgnoreCase);
        //private static Regex ampRegex = new Regex("&(?!(?:#[0-9]{2,4};|[a-z0-9]+;))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Cleans HTML fragments and documents from constructs that can lead to XSS attacks.
        /// </summary>
        /// <param name="html">Input HTML</param>
        /// <param name="isFragment">Whether given HTML is a partial fragment or a document.</param>
        /// <returns>Sanitized HTML</returns>
        public static string SanitizeHtml(string html, bool isFragment = true)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            var sanitizer = new HtmlSanitizer();

            if (isFragment)
            {
                return sanitizer.Sanitize(html);
            }
            else
            {
                return sanitizer.SanitizeDocument(html);
            }
        }

        /// <summary>
        /// Strips all tags from an HTML document or fragment.
        /// </summary>
        /// <param name="html">Input HTML</param>
        /// <returns>Clean text</returns>
        public static string StripTags(string html)
        {
            if (html.IsEmpty())
                return string.Empty;

            var removeTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "script", "style", "svg", "img" };
            var parser = new HtmlParser();

            using (var doc = parser.ParseDocument(html))
            {
                List<IElement> removeElements = new List<IElement>();

                foreach (var el in doc.All)
                {
                    if (removeTags.Contains(el.TagName))
                    {
                        removeElements.Add(el);
                    }
                }

                foreach (var el in removeElements)
                {
                    el.Remove();
                }

                return doc.Body.TextContent;
            }
        }

        /// <summary>
        /// Checks whether HTML code only contains whitespace stuff (<![CDATA[<p>&nbsp;</p>]]>)
        /// </summary>
        public static bool IsEmptyHtml(string html)
        {
            if (html.IsEmpty())
            {
                return true;
            }

            if (html.Length > 500)
            {
                // (perf) we simply assume content if length is larger
                return false;
            }

            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            using var doc = parser.ParseDocument(html);

            foreach (var el in doc.All)
            {
                switch (el.TagName.ToLower())
                {
                    case "html":
                    case "head":
                    case "br":
                        continue;
                    case "body":
                        if (el.ChildElementCount > 0)
                        {
                            continue;
                        }
                        else
                        {
                            return el.Text().Trim().IsEmpty();
                        }
                    case "p":
                    case "div":
                    case "span":
                        var text = el.Text().Trim();
                        if (text.IsEmpty() || text == "&nbsp;")
                        {
                            continue;
                        }
                        else
                        {
                            return false;
                        }
                    default:
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Replace anchor text (remove a tag from the following url <a href="http://example.com">Name</a> and output only the string "Name")
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Text</returns>
        public static string ReplaceAnchorTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = Regex.Replace(text, @"<a\b[^>]+>([^<]*(?:(?!</a)<[^<]*)*)</a>", "$1", RegexOptions.IgnoreCase);
            return text;
        }

        /// <summary>
        /// Converts plain text to HTML
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Formatted text</returns>
        public static string ConvertPlainTextToHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\r\n", "<br />");
            text = text.Replace("\r", "<br />");
            text = text.Replace("\n", "<br />");
            text = text.Replace("\t", "&nbsp;&nbsp;");
            text = text.Replace("  ", "&nbsp;&nbsp;");

            return text;
        }

        /// <summary>
        /// Converts HTML to plain text
        /// </summary>
        /// <param name="text">Text</param>
        /// <param name="decode">A value indicating whether to decode text</param>
        /// <param name="replaceAnchorTags">A value indicating whether to replace anchor text (remove a tag from the following url <a href="http://example.com">Name</a> and output only the string "Name")</param>
        /// <returns>Formatted text</returns>
        public static string ConvertHtmlToPlainText(string text, bool decode = false, bool replaceAnchorTags = false)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (decode)
                text = HttpUtility.HtmlDecode(text);

            text = text.Replace("<br>", "\n");
            text = text.Replace("<br >", "\n");
            text = text.Replace("<br />", "\n");
            text = text.Replace("&nbsp;&nbsp;", "\t");
            text = text.Replace("&nbsp;&nbsp;", "  ");

            if (replaceAnchorTags)
                text = ReplaceAnchorTags(text);

            return text;
        }

        /// <summary>
        /// Converts an attribute string spec to an html table putting each new line in a TR and each attr name/value in a TD.
        /// </summary>
        /// <param name="text">The text to convert</param>
        /// <returns>The formatted (html) string</returns>
        public static string ConvertPlainTextToTable(string text, string tableCssClass = null)
        {
            if (String.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text += "\n\n";

            var lines = text.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
            {
                return string.Empty;
            }

            using var psb = StringBuilderPool.Instance.Get(out var builder);

            builder.AppendFormat("<table{0}>", tableCssClass.HasValue() ? "class='" + tableCssClass + "'" : "");

            lines.Where(x => x.HasValue()).Each(x =>
            {
                builder.Append("<tr>");
                var tokens = x.Split(new char[] { ':' }, 2);

                if (tokens.Length > 1)
                {
                    builder.AppendFormat("<td class='attr-caption'>{0}</td>", tokens[0]);
                    builder.AppendFormat("<td class='attr-value'>{0}</td>", tokens[1]);
                }
                else
                {
                    builder.Append("<td>&nbsp;</td>");
                    builder.AppendFormat("<td class='attr-value'>{0}</td>", tokens[0]);
                }

                builder.Append("</tr>");
            });

            builder.Append("</table>");

            return builder.ToString();
        }

        /// <summary>
        /// Converts text to paragraph
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Formatted text</returns>
        public static string ConvertPlainTextToParagraph(string text)
        {
            if (String.IsNullOrEmpty(text))
                return string.Empty;

            text = _paragraphStartRegex.Replace(text, string.Empty);
            text = _paragraphEndRegex.Replace(text, "\n");
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text += "\n\n";
            text = text.Replace("\n\n", "\n");
            var strArray = text.Split(new char[] { '\n' });
            var builder = new StringBuilder();
            foreach (string str in strArray)
            {
                if ((str != null) && (str.Trim().Length > 0))
                {
                    builder.AppendFormat("<p>{0}</p>\n", str);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Converts all occurences of pixel-based inline font-size expression to relative 'em'
        /// </summary>
        /// <param name="html"></param>
        /// <param name="baseFontSizePx"></param>
        /// <returns></returns>
        public static string RelativizeFontSizes(string html, int baseFontSizePx = 16)
        {
            Guard.NotEmpty(html, nameof(html));
            Guard.IsPositive(baseFontSizePx, nameof(baseFontSizePx));

            var context = BrowsingContext.New(Configuration.Default.WithCss());
            var parser = context.GetService<IHtmlParser>();
            using var doc = parser.ParseDocument(html);

            var nodes = doc.QuerySelectorAll("*[style]");
            foreach (var node in nodes)
            {
                var styles = node.GetStyle();
                if (styles.GetFontSize() is string s && s.EndsWith("px"))
                {
                    var size = s.Substring(0, s.Length - 2).Convert<double>();
                    if (size > 0)
                    {
                        //node.Style.FontSize = Math.Round(((double)size / (double)baseFontSizePx), 4) + "em";
                        styles.SetFontSize("{0}em".FormatInvariant(Math.Round(((double)size / (double)baseFontSizePx), 4)));
                    }
                }
            }

            return doc.Body.InnerHtml;
        }
    }
}
