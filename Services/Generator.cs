namespace QuestReader.Services;

using System.Reflection;
using RazorEngineCore;

public class Generator
{
    public IRazorEngineCompiledTemplate<HtmlSafeTemplate<TemplateModel>> RazorTemplate { get; set; }

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

        var razorEngine = new RazorEngine();
        var templateFile = "page_template.cshtml";
        var baseUrl = "";
        RazorTemplate = razorEngine.Compile<HtmlSafeTemplate<TemplateModel>>(
            File.ReadAllText("page_template.cshtml"),
            option => {
                option.AddAssemblyReferenceByName("System.Collections");
                option.AddAssemblyReferenceByName("System.Private.Uri");
            }
        );

        Console.WriteLine($"Using \"{templateFile}\" with base URL {baseUrl}");
    }

    public string Run()
    {
        string result = RazorTemplate.Run(instance =>
        {
            instance.Model = new TemplateModel
            {
                Metadata = PostsSource.Metadata,
                Posts = PostsSource.Accepted,
                Now = @DateTime.UtcNow,
                //BaseUrl = "assets"
                BaseUrl = $"/static/{QuestName}",
                ToolVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"
            };
        });

        Console.WriteLine($"Template output {result.Length} bytes");
        var outputPath = Path.Join(QuestPath, "output.html");
        File.WriteAllText(outputPath, result);
        Console.WriteLine($"Wrote output to {outputPath}");
        return outputPath;
    }
}
