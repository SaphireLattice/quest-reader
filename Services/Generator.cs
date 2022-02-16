namespace QuestReader.Services;

using System.Reflection;

public class Generator
{
    public StandaloneTemplate<TemplateModel> RazorTemplate { get; set; }

    public string QuestName { get; set; }

    public PostsSource PostsSource { get; set; }

    public string QuestPath { get; set; }

    public Generator(string questName)
    {
        QuestPath = $"quests/{questName}";

        QuestName = questName;
        PostsSource = new PostsSource(questName, QuestPath);

        var chapterAnnounces = PostsSource.Metadata.Chapters.Select(c => c.Announce ?? c.Start);

        PostsSource.Accepted.Where(p => chapterAnnounces.Contains(p.Id)).ToList().ForEach(p => {
            p.IsChapterAnnounce = true;
            p.Chapter = PostsSource.Metadata.Chapters.Single(c => (c.Announce ?? c.Start) == p.Id);
        });

        var razorEngine = new RazorStandalone<StandaloneTemplate<TemplateModel>>("QuestReader");
        var templateFile = "page_template.cshtml";
        var baseUrl = "";
        RazorTemplate = razorEngine.Compile(
            "page_template.cshtml"
        ) ?? throw new Exception("No template");

        Console.WriteLine($"Using \"{templateFile}\" with base URL {baseUrl}");
    }

    public string Run()
    {
        RazorTemplate.Model =  new TemplateModel
        {
            Metadata = PostsSource.Metadata,
            Posts = PostsSource.Accepted,
            AllPosts = PostsSource.Posts,
            Now = @DateTime.UtcNow,
            BaseUrl = $"/static/{QuestName}",
            ToolVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"
        };

        var outputStream = new MemoryStream();
        RazorTemplate.ExecuteAsync(outputStream).Wait();

        var outputPath = Path.Join(QuestPath, "output.html");
        Console.WriteLine($"Template output {outputStream.Length} bytes");
        File.WriteAllBytes(outputPath, outputStream.ToArray());
        Console.WriteLine($"Wrote output to {outputPath}");
        return outputPath;
    }
}
