using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Overlay;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MixItUp.Base.Util
{
    public static class OverlayHTMLHelper
    {
        public static string SerializeJavascriptData(object value)
        {
            return JsonConvert.SerializeObject(value, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml });
        }

        public static string UpdateDefaultJavascript(OverlayItemV3Type type, string javascript)
        {
            string current;
            switch (type)
            {
                case OverlayItemV3Type.Chat: current = OverlayResources.OverlayChatDefaultJavascript; break;
                case OverlayItemV3Type.StreamBoss: current = OverlayResources.OverlayStreamBossDefaultJavascript; break;
                case OverlayItemV3Type.Goal: current = OverlayResources.OverlayGoalDefaultJavascript; break;
                case OverlayItemV3Type.Poll: current = OverlayResources.OverlayPollDefaultJavascript; break;
                default: return javascript;
            }
            string previous = type == OverlayItemV3Type.Chat
                ? current.Replace("pronouns.firstElementChild.textContent", "pronouns.firstElementChild.innerHTML")
                    .Replace("username.firstElementChild.textContent", "username.firstElementChild.innerHTML")
                : current.Replace(".textContent", ".innerHTML");
            // Saved defaults are upgraded without rewriting customized scripts.
            return string.Equals(javascript?.Replace("\r\n", "\n"), previous.Replace("\r\n", "\n"), StringComparison.Ordinal)
                ? current : javascript;
        }

        private static readonly HashSet<string> URLAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "src", "action", "formaction", "poster", "background", "data", "xlink:href"
        };

        public static async Task<string> ProcessSpecialIdentifiers(string html, CommandParametersModel parameters, bool document = false,
            IReadOnlyDictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(html)) { return html; }
            Dictionary<string, string> values = new Dictionary<string, string>();
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    string placeholder = "{" + property.Key + "}";
                    if (!html.Contains(placeholder)) { continue; }
                    string marker = "miuvalue" + Guid.NewGuid().ToString("N");
                    html = html.Replace(placeholder, marker);
                    values[marker] = await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(property.Value?.ToString(), parameters) ?? string.Empty;
                }
            }
            if (!SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(html) && values.Count == 0) { return html; }

            async Task<string> Resolve(string text, Func<string, string> encoder)
            {
                text = encoder == null
                    ? await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(text, parameters)
                    : await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(text, parameters, encoder);
                foreach (var value in values) { text = text.Replace(value.Key, encoder == null ? value.Value : encoder(value.Value)); }
                return text;
            }
            bool HasValues(string text) => SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(text) || values.Keys.Any(text.Contains);

            // Parse only the author's template. Viewer text must never be parsed as markup.
            HtmlParser parser = new HtmlParser(new HtmlParserOptions { IsScripting = true });
            using (var parsed = parser.ParseDocument(document ? html : string.Empty))
            {
                if (!document)
                {
                    foreach (INode node in parser.ParseFragment(html, parsed.Body).ToArray()) { parsed.Body.AppendChild(node); }
                }
                await ProcessNode(document ? (INode)parsed.DocumentElement : parsed.Body, Resolve, HasValues);
                return document ? "<!DOCTYPE html>\n" + parsed.DocumentElement.OuterHtml : parsed.Body.InnerHtml;
            }
        }

        private static async Task ProcessNode(INode node, Func<string, Func<string, string>, Task<string>> resolve, Func<string, bool> hasValues)
        {
            if (node is IElement element)
            {
                if (hasValues(element.LocalName)) { throw new InvalidOperationException("Overlay identifiers cannot supply element names."); }
                foreach (IAttr attribute in element.Attributes.ToArray())
                {
                    if (hasValues(attribute.Name)) { throw new InvalidOperationException("Overlay identifiers cannot supply attribute names."); }
                    string original = attribute.Value;
                    if (!hasValues(original)) { continue; }
                    string value = attribute.Name.Equals("style", StringComparison.OrdinalIgnoreCase)
                        ? await resolve(original, EncodeCSSData)
                        : await resolve(original, null);
                    if (value == original) { continue; }
                    if (attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        attribute.Name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase) ||
                        element.LocalName == "script" || element.LocalName == "object" || element.LocalName == "embed" ||
                        element.LocalName == "base" || element.LocalName == "meta" ||
                        (element.LocalName == "annotation-xml" && attribute.Name == "encoding") || IsExecutableAnimationAttribute(element, attribute.Name))
                    {
                        throw new InvalidOperationException("Overlay identifiers cannot supply executable attributes.");
                    }
                    if (URLAttributes.Contains(attribute.Name)) { ValidateURL(value); }
                    if ((element.LocalName == "iframe" || element.LocalName == "frame") && attribute.Name == "src")
                    {
                        // An embedded page must keep the origin chosen in the authored template.
                        if (!Uri.TryCreate(original, UriKind.Absolute, out Uri templateURL) ||
                            (templateURL.Scheme != Uri.UriSchemeHttp && templateURL.Scheme != Uri.UriSchemeHttps) ||
                            hasValues(templateURL.GetLeftPart(UriPartial.Authority)) ||
                            !Uri.TryCreate(value, UriKind.Absolute, out Uri resolvedURL) ||
                            !string.Equals(templateURL.GetLeftPart(UriPartial.Authority), resolvedURL.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Overlay identifiers cannot supply an embedded page origin.");
                        }
                    }
                    // The DOM serializer quotes and encodes attribute values, including unquoted templates.
                    attribute.Value = value;
                }
                if (element is IHtmlTemplateElement template) { await ProcessNode(template.Content, resolve, hasValues); }
            }
            else if (node is IText text && hasValues(text.Data))
            {
                string parent = text.ParentElement?.LocalName;
                if (parent == "script")
                {
                    text.Data = await resolve(text.Data, EncodeJavascriptData);
                }
                else if (parent == "style")
                {
                    text.Data = await resolve(text.Data, EncodeCSSData);
                }
                else
                {
                    string value = await resolve(text.Data, null);
                    if (parent == "iframe" || parent == "xmp" || parent == "noembed" || parent == "noframes" || parent == "noscript" || parent == "plaintext")
                    {
                        value = value.Replace("<", "&lt;");
                    }
                    text.Data = value;
                }
            }
            foreach (INode child in node.ChildNodes.ToArray()) { await ProcessNode(child, resolve, hasValues); }
        }

        private static string EncodeJavascriptData(string value)
        {
            // Numeric placeholders also occur outside strings. Other values remain string data.
            if (Regex.IsMatch(value, @"\A(?:-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null)\z")) { return value; }
            StringBuilder result = new StringBuilder();
            foreach (char character in value) { result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); }
            return result.ToString();
        }

        private static string EncodeCSSData(string value)
        {
            StringBuilder result = new StringBuilder();
            foreach (Rune character in value.EnumerateRunes())
            {
                if (Rune.IsLetterOrDigit(character) || " .,#%_-".Contains(character.ToString())) { result.Append(character.ToString()); }
                else { result.Append('\\').Append(character.Value.ToString("x6", CultureInfo.InvariantCulture)).Append(' '); }
            }
            return result.ToString();
        }

        private static void ValidateURL(string value)
        {
            string normalized = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
            Match scheme = Regex.Match(normalized, @"\A([a-z][a-z0-9+.-]*):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (scheme.Success && !new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeFile, Uri.UriSchemeMailto }
                .Contains(scheme.Groups[1].Value, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Overlay identifiers cannot supply executable URLs.");
            }
        }

        private static bool IsExecutableAnimationAttribute(IElement element, string attribute)
        {
            if (element.NamespaceUri != "http://www.w3.org/2000/svg" ||
                !(element.LocalName.StartsWith("animate", StringComparison.OrdinalIgnoreCase) || element.LocalName == "set")) { return false; }
            string target = element.GetAttribute("attributeName") ?? string.Empty;
            return attribute.Equals("attributeName", StringComparison.OrdinalIgnoreCase) ||
                ((attribute == "from" || attribute == "to" || attribute == "values") &&
                (URLAttributes.Contains(target) || target.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                target.Equals("style", StringComparison.OrdinalIgnoreCase) || target.Equals("srcdoc", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
