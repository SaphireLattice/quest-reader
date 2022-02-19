using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace QuestReader.Models.ParsedContent;

public class ParsedContent
{
    public Version Version { get; set; }
    public IList<ContentNode> Nodes { get; set; }
}

class ContentConverter : JsonConverter<ContentNode>
{
    public override ContentNode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        throw new NotImplementedException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        ContentNode value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                JsonSerializer.Serialize(writer, null as ContentNode, options);
                break;
            default:
                {
                    if (value is RootNode)
                        throw new InvalidDataContractException("RootNode must not be used");
                    var type = value.GetType();

                    JsonSerializer.Serialize(writer, value, type, options);
                    break;
                }
        }
    }
}

[JsonConverter(typeof(ContentConverter))]
public abstract class ContentNode
{
    public string Type { get => GetType().Name.Replace("Node", ""); }

    public virtual string Render(TemplateModel model)
    {
        throw new NotImplementedException("Rendering is not supported for this node type");
    }
}

public class TextNode : ContentNode
{
    public string Text { get; set; }

    public override string ToString() => $"\"{Text}\"";

    public override string Render(TemplateModel model) => HttpUtility.HtmlEncode(Text);
}

public class NewlineNode : ContentNode
{
    public override string ToString() => $"<br>";

    public override string Render(TemplateModel model) => "<br>";
}

public class ReferenceNode : ContentNode
{
    public int PostId { get; set; }
    public int ThreadId { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public bool LongReference { get; set; }
}

public enum ReferenceType
{
    QuestActive,
    QuestArchive,
    QuestDiscussion
}

public abstract class ContainerNode : ContentNode
{
    public IList<ContentNode> Nodes { get; set; }

    public override string ToString() => $"{Type} [ {string.Join(",\n", Nodes)} ]";
}

// A temporary container to recursively parse everything of a note before bailing and MUST NOT BE USED NORMALLY
public class RootNode : ContainerNode
{
    public override string ToString() => throw new InvalidDataContractException("RootNode must not be used");

    public override string Render(TemplateModel model) => throw new InvalidDataContractException("RootNode must not be used");
}

public class QuoteNode : ContainerNode { };

public class BoldNode : ContainerNode { };

public class ItalicsNode : ContainerNode { };

public class StrikeoutNode : ContainerNode { };

public class SpoilerNode : ContainerNode { };

public class InlineCodeNode : ContainerNode { };

public class UnderlineNode : ContainerNode { };

public class ExternalLinkNode : ContainerNode
{
    public string Destination { get; set; }
}