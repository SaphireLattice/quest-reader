namespace QuestReader.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class PostsSource
{
    public List<ThreadPost> Posts { get; set; }

    public List<ThreadPost> Accepted { get; set; }

    public Metadata Metadata { get; set; }

    public PostsSource(string questName, string basePath)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        using var fileStream = File.OpenRead(Path.Combine(basePath, "metadata.json"));
        Metadata = JsonSerializer.Deserialize<Metadata>(fileStream, options)
                ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
        fileStream.Dispose();

        Console.Out.WriteLine($"Loaded metadata: {Metadata}");
        Posts = Metadata.Threads.SelectMany(tId => {
            using var fileStream = File.OpenRead(Path.Combine(basePath, $"thread_{tId}.json"));
            var threadData = JsonSerializer.Deserialize<List<ThreadPost>>(fileStream, options)
                ?? throw new InvalidDataException("Empty deserialisation result for thread data");
            fileStream.Dispose();

            return threadData;
        }).ToList();

        using var postsListStream = File.OpenRead(Path.Combine(basePath, "accepted.json"));
        var ids = JsonSerializer.Deserialize<List<int>>(postsListStream, options)
                ?? throw new InvalidDataException("Empty deserialisation result for quest metadata");
        Accepted = Posts.Where(p => ids.Contains(p.Id)).ToList();
        Console.Out.WriteLine($"Loaded a list of {Accepted.Count} posts, referencing {Accepted.Where(a => a.File is not null).Count()} files");

        var rx = new Regex(@"data-post-ref=""(\d+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        foreach (var post in Posts)
        {
            var matches = rx.Matches(post.RawHtml);
            if (!matches.Any())
                continue;

            post.RepliesTo = new List<int>();
            foreach (Match match in matches)
            {
                var replyId = int.Parse(match.Groups[1].Value);
                var found = Posts.FirstOrDefault(p => p.Id == replyId);
                if (found is null)
                    continue;
                post.RepliesTo.Add(replyId);
            }
        }
    }
}