namespace QuestReader.Models;

public class TemplateModel
{
    public Metadata Metadata { get; set; }
    public DateTime Now { get; set; }
    public List<ThreadPost> Posts { get; set; }
    public List<ThreadPost> AllPosts { get; set; }
    public string AssetsPath { get; set; }
    public string ToolVersion { get; set; }
}