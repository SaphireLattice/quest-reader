namespace QuestReader.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QuestReader.Models;
using SixLabors.ImageSharp;

public class PostsSource
{
    public List<ThreadPost> Posts { get; set; }

    public HashSet<ThreadPost> Accepted { get; set; }

    public Metadata Metadata { get; set; }

    public string BasePath { get; set; }

    public PostsSource(string questName)
    {
        BasePath = $"quests/{questName}";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        using var fileStream = File.OpenRead(Path.Combine(BasePath, "metadata.json"));
        Metadata = JsonSerializer.Deserialize<Metadata>(fileStream, options)
                ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
        fileStream.Dispose();

        Console.Out.WriteLine($"Loaded metadata: {Metadata}");
        Posts = Metadata.Threads
            .SelectMany(tId => QuestdenParse.GetThread(tId, BasePath).Result)
            .ToList();

        using var postsListStream = File.OpenRead(Path.Combine(BasePath, "accepted.json"));
        var ids = JsonSerializer.Deserialize<List<int>>(postsListStream, options)
                ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
        Accepted = Posts.Where(p => ids.Contains(p.Id)).ToHashSet();

        foreach (var chapter in Metadata.Chapters)
        {
            var post = Accepted.Single(p => p.Id == (chapter.Announce ?? chapter.Start));
            post.IsChapterAnnounce = true;
            post.Chapter = chapter;
        }

        Console.Out.WriteLine($"Loaded a list of {Accepted.Count} posts, referencing {Accepted.Where(a => a.File is not null).Count()} files");

        foreach (var post in Accepted)
        {
            post.AuthorPost = true;
            if (post.ParsedContent is null || post.ParsedContent.Version < QuestdenParse.LatestCompatibleVersion)
                throw new NotImplementedException("Repairing missing post content or updating it is not implemented yet");
        }

        var referenced = Accepted.SelectMany(p => p.ParsedContent!.GetReferences());
        Accepted.UnionWith(Posts.Where(p => referenced.Contains(p.Id)));
        Accepted = Accepted.OrderBy(p => p.Id).ToHashSet();

        FileDownloader.DownloadList(BasePath, Metadata.AssetsBaseUrl, Accepted.Where(p => p.File is not null).Select(p => p.File!)).Wait();

        foreach (var post in Accepted.Where(f => f.File is not null))
        {
            using var imageStream = File.OpenRead(Path.Combine(BasePath, "assets", post.File!));
            IImageInfo imageInfo = Image.Identify(imageStream);
            if (imageInfo is null) {
                Console.Out.WriteLine($"Not a valid image: {post.File!}");
                continue;
            }
            post.FileHeight = imageInfo.Height;
            post.FileWidth = imageInfo.Width;
        }

        Console.Out.WriteLine($"Done loading with {Accepted.Count} posts, referencing {Accepted.Where(a => a.File is not null).Count()} files");
    }
}