using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace QuestReader.Models.ParsedContent;

[JsonConverter(typeof(ContentConverter))]
public abstract class ContentNode
{
    public string Type { get => GetType().Name.Replace("Node", ""); }
}

public abstract class ContainerNode : ContentNode
{
    public IList<ContentNode> Nodes { get; set; }

    public override string ToString() => $"{Type} [ {string.Join(",\n", Nodes)} ]";

    public IEnumerable<int> GetReferences()
    {
        return Nodes.SelectMany(n =>
            n is ContainerNode container
                ? container.GetReferences()
                : (
                    n is ReferenceNode @ref ? new List<int> { @ref.PostId ?? @ref.ThreadId } : Array.Empty<int>()
                )
        );
    }
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
        switch (value) {
            case null:
                JsonSerializer.Serialize(writer, null as ContentNode, options);
                break;
            case TextNode textNode:
                JsonSerializer.Serialize(writer, textNode.Text, options);
                break;
            default:
                var type = value.GetType();
                JsonSerializer.Serialize(writer, value, type, options);
                break;
        };
    }
}

public class RootNode : ContainerNode
{
    public Version Version { get; set; }
}

public class TextNode : ContentNode
{
    public string Text { get; set; }

    public override string ToString() => $"{Text}";
}

public class NewlineNode : ContentNode
{
    public override string ToString() => $"\n";
}


public class ReferenceNode : ContentNode
{
    public int? PostId { get; set; }
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

public class YoutubeEmbedNode : ContentNode
{
    /// <remarks>Todo: Make this a URL</remarks>
    public string VideoLink { get; set; }
};

public class QuoteNode : ContainerNode { };

public class BoldNode : ContainerNode { };

public class ItalicsNode : ContainerNode { };

public class StrikeoutNode : ContainerNode { };

public class SpoilerNode : ContainerNode { };

public class InlineCodeNode : ContainerNode { };

public class UnderlineNode : ContainerNode { };

public class SmallFontNode : ContainerNode { };

public class ColorNode : ContainerNode
{
    public string Color { get; set; }
};

public class ExternalLinkNode : ContainerNode
{
    /// <remarks>Todo: Make this a URL</remarks>
    public string Destination { get; set; }
}