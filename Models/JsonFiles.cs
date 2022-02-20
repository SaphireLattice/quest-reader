namespace QuestReader.Models;

using System.Text.Json.Serialization;
using QuestReader.Models.ParsedContent;

public record ThreadPost
{
    public int Id { get; set; }
    public string Author { get; set; }
    public string Uid { get; set; }
    public string RawHtml { get; set; }
    public RootNode? ParsedContent { get; set; }
    public string? File { get; set; }
    public string? Filename { get; set; }
    public string? Title { get; set; }
    public string? Tripcode { get; set; }
    public DateTime Date { get; set; }

    [JsonIgnore]
    public bool IsChapterAnnounce { get; set; } = false;
    [JsonIgnore]
    public ChapterMetadata? Chapter { get; set; }
    [JsonIgnore]
    public bool AuthorPost { get; set; } = false;
}

public record Metadata
{
    public string Name { get; set; }
    public string Author { get; set; }
    public Uri AuthorPage { get; set; }
    public string? AuthorTwitter { get; set; }
    public string? Description { get; set; }
    public Uri AssetsBaseUrl { get; set; }
    public string SocialPreview { get; set; }
    public List<int> Threads { get; set; }
    public List<ChapterMetadata> Chapters { get; set; }

    public override string ToString() => $"\"{Name}\" by {Author}";
}
public record ChapterMetadata
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Subtitle { get; set; }
    public int Start { get; set; }
    public int? Announce { get; set; }
    public int End { get; set; }
}