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
    public static readonly Version LatestCompatibleVersion = new(1, 0, 2);

    static Regex RefRegex { get; } = new Regex(@"^ref\|(questarch|questdis|quest)\|(\d+)\|(\d+)$", RegexOptions.Compiled);

    static Regex LongRefRegex { get; } = new Regex(@"(?:https?://)?(?:www.)?(?:tgchan|questden).org/kusaba/(questarch|questdis|quest)/res/(\d+).html#?i?(\d+)?$", RegexOptions.Compiled);

    static Regex DateRegex { get; } = new Regex(@"(\d{4,4})\/(\d\d)\/(\d\d)\(\w+\)(\d\d):(\d\d)", RegexOptions.Compiled);

    static Regex FilenameRegex { get; } = new Regex(@"File \d+\.[^ ]+ - \([\d\.KMG]+B , \d+x\d+ , (.*) \)", RegexOptions.Compiled);

    public static async Task<IEnumerable<ThreadPost>> GetThread(int threadId, string destinationPath)
    {
        var url = $"http://questden.org/kusaba/quest/res/{threadId}.html";
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        var doc = new HtmlDocument
        {
            OptionEmptyCollection = true
        };

        // Todo: check if the thread data & parsed entity is of same version
        if (File.Exists(Path.Join(destinationPath, $"thread_{threadId}.json")))
            return JsonSerializer.Deserialize<IEnumerable<ThreadPost>>(File.ReadAllText("asd"), options)
                ?? throw new NullReferenceException("No data loaded");

        var cacheDir = Path.Join(destinationPath, "cache");
        var cacheFile = Path.Join(cacheDir, $"QuestDen-{threadId}.html");
        if (!File.Exists(cacheFile))
        {
            var httpClient = new HttpClient();
            var content = await httpClient.GetStringAsync(url);
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            File.WriteAllText(cacheFile, content);
            doc.LoadHtml(content);
        }
        else
            doc.LoadHtml(File.ReadAllText(cacheFile));

        var nodes = doc.DocumentNode.SelectNodes(".//*[@class='reply']|.//form[@id='delform']");

        var posts = new List<ThreadPost>();
        foreach (var node in nodes)
        {
            var post = ParsePost(node, threadId);
            posts.Add(post);
        }

        return posts;
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
            ?.Attributes["href"].DeEntitizeValue.Replace("/kusaba/questarch/src/", "").Trim();

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

    public static RootNode ParseContent(string postHtml)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(postHtml);
        var parseResult = RecursiveParse(htmlDoc.DocumentNode);
        if (parseResult is not RootNode rootNode)
            throw new Exception("Parsing returned a non-RootNode root");
        return rootNode;
    }

    private static ContentNode RecursiveParse(HtmlNode node, ContentNode? parentNode = null)
    {
        if (node is null)
            throw new NullReferenceException("Html node is null");

        if (node is HtmlTextNode textNode) {
            var decoded = HttpUtility.HtmlDecode(textNode.Text.Trim());
            if (parentNode is QuoteNode)
                decoded = Regex.Replace(decoded, @"^>\s*", "");
            return new TextNode { Text = decoded };
        }

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
                        PostId = int.Parse((match.Groups[3]?.Success ?? false) ? match.Groups[3].Value : match.Groups[2].Value),
                        ThreadId = int.Parse(match.Groups[2].Value),
                        ReferenceType = match.Groups[1].Value switch
                        {
                            "quest" => ReferenceType.QuestActive,
                            "questarch" => ReferenceType.QuestArchive,
                            "questdis" => ReferenceType.QuestDiscussion,
                            _ => throw new InvalidDataException(""),
                        },
                        LongReference = true
                    },
                "a" when !node.GetClasses().Any() => new ExternalLinkNode { Destination = node.GetAttributeValue("href", "ERROR") },
                "br" => new NewlineNode { },
                "#document" => new RootNode { Version = LatestCompatibleVersion },
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
                    node.GetAttributes() is var attributes
                    && attributes.Count() == 1
                    && attributes.Single() is var maybeStyle
                    && maybeStyle.Name == "style"
                    && maybeStyle.DeEntitizeValue == @"font-size:small;"
                    => new SmallFontNode { },
                "span" when
                    node.GetAttributes() is var attributes
                    && attributes.Count() == 1
                    && attributes.Single() is var maybeStyle
                    && maybeStyle.Name == "style"
                    // Let's hope nobody used any colors beyond the hex ones...
                    // But probably will need to add support for that. Eh, later!
                    && Regex.Match(maybeStyle.DeEntitizeValue, @"^color:\s*(#[0-9a-f]{3,8});?$", RegexOptions.IgnoreCase) is var match
                    && match is not null
                    && match.Success
                    => new ColorNode { Color = match.Groups[1].Value },
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
                    => new YoutubeEmbedNode { VideoLink = innerNode.GetAttributes().Single(a => a.Name == "src").DeEntitizeValue },
                // I have seen both being used but I am not sure as to the difference. Different software version?
                "div" or "span" when
                    node.GetAttributes() is var attributes
                    && attributes.Count() == 1
                    && attributes.Single() is var maybeStyle
                    && maybeStyle.Name == "style"
                    && maybeStyle.DeEntitizeValue == @"white-space: pre-wrap !important; font-family: monospace, monospace !important;"
                    => new InlineCodeNode { },
                _ => throw new InvalidDataException($"Unknown node parse attempt: {node.Name} #{node.Id} .{string.Join(".", node.GetClasses())}\n{node.OuterHtml}")
            };
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
