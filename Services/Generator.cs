namespace QuestReader.Services;

using System.Reflection;
using QuestReader.Models;

public class Generator
{
    public StandaloneTemplate<TemplateModel> RazorTemplate { get; set; }

    public string QuestName { get; set; }

    public PostsSource PostsSource { get; set; }

    public string AssetsPath { get; set; }

    public string OutputPath { get; set; }

    public Generator(string questName)
    {
        QuestName = questName;
        AssetsPath = $"/static/{questName}";
        PostsSource = new PostsSource(questName);

        var razorEngine = new RazorStandalone<StandaloneTemplate<TemplateModel>>("QuestReader");
        var templateFile = "page_template.cshtml";
        RazorTemplate = razorEngine.Compile(
            "page_template.cshtml"
        ) ?? throw new Exception("No template");

        Console.WriteLine($"Using \"{templateFile}\" with base URL {AssetsPath}");
    }

    public string Run()
    {
        RazorTemplate.Model = new TemplateModel
        {
            Metadata = PostsSource.Metadata,
            Posts = PostsSource.Accepted.ToList(),
            AllPosts = PostsSource.Posts,
            Now = @DateTime.UtcNow,
            AssetsPath = AssetsPath.TrimEnd('/'), // Strip trailing slash
            ToolVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"
        };

        var outputStream = new MemoryStream();
        RazorTemplate.ExecuteAsync(outputStream).Wait();

        var outputPath = Path.Join(OutputPath ?? PostsSource.BasePath, "output.html");
        Console.WriteLine($"Template output {outputStream.Length} bytes");
        File.WriteAllBytes(outputPath, outputStream.ToArray());
        Console.WriteLine($"Wrote output to {outputPath}");
        return outputPath;
    }
}
