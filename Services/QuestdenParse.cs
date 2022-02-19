using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using QuestReader.Models;
using QuestReader.Models.ParsedContent;

namespace QuestReader.Services;

public class QuestdenParse
{
    static readonly Version LatestCompatibleVersion = new(1, 0, 2);

    static Regex RefRegex { get; } = new Regex(@"^ref\|(questarch|questdis|quest)\|(\d+)\|(\d+)$", RegexOptions.Compiled);

    static Regex LongRefRegex { get; } = new Regex(@"(?:https?://)?(www.)?(tgchan|questden).org/kusaba/(questarch|questdis|quest)/res/(\d+).html#?i?(\d+)?$", RegexOptions.Compiled);

    static Regex DateRegex { get; } = new Regex(@"(\d{4,4})\/(\d\d)\/(\d\d)\(\w+\)(\d\d):(\d\d)", RegexOptions.Compiled);

    static Regex FilenameRegex { get; } = new Regex(@"File \d+\.[^ ]+ - \([\d\.KMG]+B , \d+x\d+ , (.*) \)", RegexOptions.Compiled);

    public static async Task GetThread(int threadId)
    {
        var url = $"http://questden.org/kusaba/quest/res/{threadId}.html";
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        var doc = new HtmlDocument();
        doc.OptionEmptyCollection = true;

        if (File.Exists($"thread_{threadId}.json"))
            return;

        var cacheFile = $"cache/QuestDen-{threadId}.html";
        if (!File.Exists(cacheFile))
        {
            var httpClient = new HttpClient();
            var content = await httpClient.GetStringAsync(url);
            if (!Directory.Exists("cache"))
                Directory.CreateDirectory("cache");
            File.WriteAllText(cacheFile, content);
            doc.LoadHtml(content);
        }
        else
        {
            doc.LoadHtml(File.ReadAllText(cacheFile));
        }

        var nodes = doc.DocumentNode.SelectNodes(".//*[@class='reply']|.//form[@id='delform']");

        var posts = new List<ThreadPost>();
        foreach (var node in nodes)
        {
            var post = ParsePost(node, threadId);
            posts.Add(post);
            //var postJson = JsonSerializer.Serialize(post);
            //Console.Out.WriteLine($"{postJson}\n");
        }
        File.WriteAllText($"thread_{threadId}.json", JsonSerializer.Serialize(posts, options));
    }
    public static ThreadPost ParsePost(string postHtml, int threadId)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(postHtml);
        return ParsePost(htmlDoc.DocumentNode.FirstChild, threadId);
    }

    public static ThreadPost ParsePost(HtmlNode postNode, int threadId)
    {
        var post = new ThreadPost { };

        var id = postNode
            .SelectNodes("./div[@class='postwidth']/a[@name!='s']")
            .Single()
            .Attributes["name"].Value.Trim();
        post.Id = id == "s" ? threadId : int.Parse(id);
        post.Title = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='filetitle']")
            .SingleOrDefault()
            ?.InnerText.Trim();
        post.Author = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='postername']")
            .Single()
            .InnerText.Trim();
        post.Uid = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='uid']")
            .Single()
            .InnerText.Trim()
            .Replace("ID: ", "", true, CultureInfo.InvariantCulture);
        post.Date = DateTime.Parse(
            DateRegex.Replace(postNode
                .SelectNodes("./div[@class='postwidth']/label/text()[last()]")
                .Single()
                .InnerText.Trim(),
                "$1-$2-$3T$4:$5"
            ),
            null,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        post.File = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='filesize']/a")
            .SingleOrDefault()
            ?.Attributes["href"].Value.Trim();

        var filenameRaw = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='filesize']")
            .SingleOrDefault()
            ?.InnerText.Trim();
        if (filenameRaw is not null)
        {
            filenameRaw = Regex.Replace(filenameRaw, @"\s*\n\s*", " ", RegexOptions.Multiline);
            post.Filename = filenameRaw.Contains("File ") ?
                FilenameRegex.Match(filenameRaw)?.Groups[1]?.Value
                ?? null : null;
        }

        post.Tripcode = postNode
            .SelectNodes("./div[@class='postwidth']//*[@class='postertrip']")
            .SingleOrDefault()
            ?.InnerText.Trim();
        post.RawHtml = Regex.Replace(
            postNode
                .SelectNodes("./blockquote")
                .Single()
                .InnerHtml
                .Replace("\r", " ")
                .Replace(@"<div style=""display:inline-block; width:400px;""></div><br>", "")
                .Trim(),
            @"\s*<br\s*\/?>$",
            ""
        );
        try
        {
            post.ParsedContent = ParseContent(post.RawHtml);
        }
        catch (FormatException)
        {
            Console.WriteLine($"\n{post.Id} {post.RawHtml.Replace("\r", "")}\n");
            throw;
        }

        return post;
    }

    public static ParsedContent ParseContent(string postHtml)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(postHtml);
        var rootNode = RecursiveParse(htmlDoc.DocumentNode);
        if (rootNode is not RootNode)
            throw new Exception("Parsing returned a non-RootNode root");
        return new ParsedContent
        {
            Version = LatestCompatibleVersion,
            Nodes = ((RootNode)rootNode).Nodes
        };
    }

    private static ContentNode RecursiveParse(HtmlNode node, ContentNode? parentNode = null)
    {
        if (node is null)
            throw new NullReferenceException("Html node is null");

        if (node is HtmlTextNode textNode)
            return new TextNode { Text = HttpUtility.HtmlDecode(textNode.Text.Trim()) };

        if (node.NodeType is HtmlNodeType.Document or HtmlNodeType.Element)
        {
            ContentNode outNode = node.Name.ToLowerInvariant() switch
            {
                "a" when
                    node.GetClasses().Count() == 1
                    && node.FirstChild?.NodeType == HtmlNodeType.Text
                    && node.Descendants().Count() == 1
                    && node.GetClasses().Single() is var classname
                    && RefRegex.Match(classname) is var match && match is not null
                    && match.Success
                    && HttpUtility.HtmlDecode(node.FirstChild?.InnerText) is var innerText && innerText is not null
                    && (innerText == $">>{match.Groups[3].Value}" || innerText == $">>/{match.Groups[1].Value}/{match.Groups[3].Value}")
                    => new ReferenceNode
                    {
                        PostId = int.Parse(match.Groups[3].Value),
                        ThreadId = int.Parse(match.Groups[2].Value),
                        ReferenceType = match.Groups[1].Value switch
                        {
                            "quest" => ReferenceType.QuestActive,
                            "questarch" => ReferenceType.QuestArchive,
                            "questdis" => ReferenceType.QuestDiscussion,
                            _ => throw new InvalidDataException(""),
                        },
                        LongReference = false
                    },
                "a" when
                    !node.GetClasses().Any()
                    && node.FirstChild is HtmlTextNode firstNode && firstNode is not null
                    && node.Descendants().Count() == 1
                    && HttpUtility.HtmlDecode(firstNode.Text) is var nodeText
                    && node.GetAttributeValue("href", "ERROR") == nodeText
                    && LongRefRegex.Match(nodeText) is var match && match is not null
                    && match.Success
                    => new ReferenceNode
                    {
                        PostId = int.Parse((match.Groups[5]?.Success ?? false) ? match.Groups[5].Value : match.Groups[4].Value),
                        ThreadId = int.Parse(match.Groups[4].Value),
                        LongReference = true
                    },
                "a" when !node.GetClasses().Any() => new ExternalLinkNode { Destination = node.GetAttributeValue("href", "ERROR") },
                "br" => new NewlineNode { },
                "#document" => new RootNode { },
                "i" => new ItalicsNode { },
                "b" => new BoldNode { },
                "strike" => new StrikeoutNode { },
                "span" when
                    node.GetClasses() is var classes
                    && classes.Count() == 1
                    && classes.Single() == "spoiler" => new SpoilerNode { },
                "span" when
                    node.GetClasses() is var classes
                    && classes.Count() == 1
                    && classes.Single() == "unkfunc" => new QuoteNode { },
                "span" when
                    node.GetAttributes() is var attributes
                    && attributes.Count() == 1
                    && attributes.Single() is var maybeStyle
                    && maybeStyle.Name == "style"
                    && maybeStyle.DeEntitizeValue == @"border-bottom: 1px solid"
                    => new UnderlineNode { },
                "span" when
                    node.Descendants().Where(
                        d => d is not HtmlTextNode
                            || (d is HtmlTextNode textNode
                            && !string.IsNullOrWhiteSpace(textNode.Text.Trim()))
                    ) is var descendants
                    && descendants.Count() == 1
                    && descendants.Single() is HtmlNode innerNode
                    && innerNode.Name == "iframe"
                    && innerNode.GetAttributeValue("src", null).Contains("youtube")
                    => new TextNode { Text = $"Here be youtube link {innerNode.GetAttributeValue("src", null)}"},
                "div" when
                    node.GetAttributes() is var attributes
                    && attributes.Count() == 1
                    && attributes.Single() is var maybeStyle
                    && maybeStyle.Name == "style"
                    && maybeStyle.DeEntitizeValue == @"white-space: pre-wrap !important; font-family: monospace, monospace !important;"
                    => new InlineCodeNode { },
                _ => throw new InvalidDataException($"Unknown node parse attempt: {node.Name} #{node.Id} .{string.Join(".", node.GetClasses())}\n{node.OuterHtml}")
            };
            //if (outNode is ExternalLinkNode refNode)
            //Console.Out.WriteLine($"Refnode: {string.Join(", ", node.GetClasses())} {node.OuterHtml}");
            //Console.Out.WriteLine($"{node.Name}: {outNode.GetType().Name} {outNode is ContainerNode} {node.ChildNodes.Count} children, {node.Descendants().Count()} descendants");
            if (outNode is ContainerNode container)
            {
                container.Nodes = node.ChildNodes
                    .Select(n => RecursiveParse(n, container))
                    .Where(n => n is not TextNode || (n is TextNode textNode && !string.IsNullOrWhiteSpace(textNode.Text)))
                    .ToList();
            }
            return outNode;
        }

        throw new Exception("Unsupported HTML node type");
    }
}
