namespace QuestReader.Extensions;

using System.Web;
using QuestReader.Models;
using QuestReader.Models.ParsedContent;

public static class RenderContentHtmlExtension
{
    public static string RenderContentHtml(this ContentNode node, TemplateModel model)
    {
        return node switch
        {
            RootNode rootNode => string.Join("", rootNode.Nodes.Select(n => n.RenderContentHtml(model))),
            TextNode textNode => textNode.Text,
            NewlineNode => "<br>\n",
            ReferenceNode refNode => refNode.RenderReferenceHtml(model),
            ContainerNode container => container.RenderContailerHtml(model),
            _ => throw new NotImplementedException(),
        };
    }


    static string RenderContailerHtml(this ContainerNode node, TemplateModel model)
    {
        var tag = node switch
        {
            QuoteNode => "span",
            BoldNode => "strong",
            ItalicsNode => "em",
            StrikeoutNode => "s",
            UnderlineNode => "span",
            SpoilerNode => "span",
            ExternalLinkNode => "a",
            null => throw new NullReferenceException("Node is null, something is ver wrong"),
            _ => throw new NotImplementedException($"Rendering not implemented for {node.GetType().Name}"),
        };

        string? extra = node switch
        {
            QuoteNode quote => " class=\"text-quote\"",
            UnderlineNode => " class=\"text-underline\"",
            SpoilerNode => " class=\"text-spoiler\"",
            ExternalLinkNode externalLink => $" href=\"{HttpUtility.HtmlAttributeEncode(externalLink.Destination)}\"",
            _ => null,
        };
        var content = string.Join("", node.Nodes.Select(n => n.RenderContentHtml(model)));

        // The spaces around this are absolutely stopgap and will NOT work for something like "abso<em>-fucking-</em>lutely"
        // HTML is... frustrating
        // Note for future implementation: don't trim, just collapse to single space
        // Search regex "[^>\s;]<[^>]>". And yes there /are/ hits. Fuck!
        return $" <{tag}{extra}>{content}</{tag}> ";
    }

    static string RenderReferenceHtml(this ReferenceNode node, TemplateModel model)
    {
        var type = node.ReferenceType switch
        {
            ReferenceType.QuestActive => "quest",
            ReferenceType.QuestArchive => "quest-archived",
            ReferenceType.QuestDiscussion => "discussion",
            _ => throw new NotImplementedException(),
        };

        var included = model.Metadata.Threads.Any(t => t == node.ThreadId) && model.Posts.Any(p => p.Id == node.PostId);
        var href = "";
        if (included)
            href = $"href=\"#post-{node.PostId}\" ";

        return $"<a {href}class=\"post-reference\" data-thread=\"{node.ThreadId}\" data-post=\"{node.PostId}\" data-type=\"{type}\">&gt;&gt;{node.PostId}</a>";
    }
}