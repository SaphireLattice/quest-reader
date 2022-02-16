using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace QuestReader;

public class RazorStandalone<TTemplate>
{
    RazorProjectEngine Engine { get; set; }
    public RazorStandalone(string @namespace)
    {
        var fs = RazorProjectFileSystem.Create(".");

        Engine = RazorProjectEngine.Create(RazorConfiguration.Default, fs, (builder) =>
        {
            builder.SetNamespace(@namespace);
            builder.SetBaseType(typeof(TTemplate).FullName);
            builder.AddTargetExtension(new TemplateTargetExtension()
            {
                TemplateTypeName = "RazorStandaloneHelperResult",
            });
        });
    }

    public TTemplate? Compile(string filename)
    {
        var doc = RazorSourceDocument.Create(File.ReadAllText("page_template.cshtml"), Path.GetFileName(filename));

        var codeDocument = Engine.Process(doc, null, new List<RazorSourceDocument>(), new List<TagHelperDescriptor>());
        var cs = codeDocument.GetCSharpDocument();
        var tree = CSharpSyntaxTree.ParseText(cs.GeneratedCode, new CSharpParseOptions(LanguageVersion.Latest));

        var dllName = Path.GetFileNameWithoutExtension(filename) + ".dll";
        var assemblies = new[]
        {
            Assembly.Load(new AssemblyName("netstandard")),
            typeof(object).Assembly,
            typeof(Uri).Assembly,
            Assembly.Load(new AssemblyName("System.Runtime")),
            Assembly.Load(new AssemblyName("System.Collections")),
            Assembly.Load(new AssemblyName("System.Linq")),
            Assembly.Load(new AssemblyName("System.Linq.Expressions")),
            Assembly.Load(new AssemblyName("Microsoft.CSharp")),
            Assembly.GetExecutingAssembly(),
        };

        var compilation = CSharpCompilation.Create(dllName, new[] { tree },
            assemblies.Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToArray(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var memoryStream = new MemoryStream();

        var result = compilation.Emit(memoryStream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        if (!result.Success)
        {
            Console.WriteLine(string.Join(Environment.NewLine, result.Diagnostics));
            throw new Exception("Welp");
        }

        var asm = Assembly.Load(memoryStream.ToArray());
        var templateInstance = (TTemplate?) Activator.CreateInstance(asm.GetType("QuestReader.Template"));
        if (templateInstance is null)
            throw new Exception("Template is null");

        return templateInstance;
    }
}

public interface IHelper
{
    public void WriteTo(TextWriter writer);
}

public class RazorStandaloneHelperResult : IHelper
{
    public Action<TextWriter> WriteAction { get; }

    public RazorStandaloneHelperResult(Action<TextWriter> action)
    {
        WriteAction = action;
    }

    public void WriteTo(TextWriter writer)
    {
        WriteAction(writer);
    }

    public override string ToString()
    {
        using var buffer = new MemoryStream();
        using var writer = new StreamWriter(buffer, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), 4096, leaveOpen: true);
        WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

class RawContent : IHelper
{
    public object Value { get; set; }

    public RawContent(object value)
    {
        Value = value;
    }

    public void WriteTo(TextWriter writer)
    {
        writer.Write(Value);
    }

    public override string ToString() => Value?.ToString() ?? "";
}

public abstract class StandaloneTemplate<TModel>
{
    public TModel Model { get; set; }

    protected StreamWriter Output { get; set; }

    public async Task WriteLiteralAsync(string literal)
    {
        await Output.WriteAsync(literal);
    }

    string? Suffix {get;set;}

    public async Task BeginWriteAttributeAsync(
        string name,
        string prefix, int prefixOffset,
        string suffix, int suffixOffset,
        int attributeValuesCount
    )
    {
        Suffix = suffix;
        await WriteLiteralAsync(prefix);
    }
    public async Task WriteAttributeValueAsync(string prefix, int prefixOffset, object? value, int valueOffset, int valueLength, bool isLiteral)
    {
        await WriteLiteralAsync(prefix);
        await WriteAsync(value);
    }

    public async Task EndWriteAttributeAsync() {
        await WriteLiteralAsync(Suffix!);
        Suffix = null;
    }

    public async Task WriteAsync(object? obj)
    {
        if (obj is not null and IHelper helper)
            helper.WriteTo(Output);
        else
            await Output.WriteAsync(System.Web.HttpUtility.HtmlEncode(obj));
        Output.Flush();
    }

    public async Task ExecuteAsync(Stream stream)
    {
        // We technically don't need this intermediate buffer if this method accepts a memory stream.
        //var buffer = new MemoryStream();
        Output = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), 4096, leaveOpen: true);
        await ExecuteAsync();
        await Output.FlushAsync();
        await Output.DisposeAsync();
        //buffer.Seek(0, SeekOrigin.Begin);
        //await buffer.CopyToAsync(stream);
    }

    public void PushWriter(TextWriter writer)
    {

    }

    public StreamWriter PopWriter()
    {
        return Output;
    }

    public virtual Task ExecuteAsync()
    {
        throw new NotImplementedException();
    }

    public void Write(object? obj)
    {
        WriteAsync(obj).Wait();
    }
    public void WriteLiteral(string literal)
    {
        WriteLiteralAsync(literal).Wait();
    }
    public void BeginWriteAttribute(string name, string prefix, int prefixOffset, string suffix, int suffixOffset, int attributeValuesCount)
    {
        BeginWriteAttributeAsync(name, prefix, prefixOffset, suffix, suffixOffset, attributeValuesCount).Wait();
    }
    public void WriteAttributeValue(string prefix, int prefixOffset, object value, int valueOffset, int valueLength, bool isLiteral)
    {
        WriteAttributeValueAsync(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral).Wait();
    }
    public void EndWriteAttribute()
    {
        EndWriteAttributeAsync().Wait();
    }

    public static object Raw(object value)
    {
        return new RawContent(value);
    }
}