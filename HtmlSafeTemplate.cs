using RazorEngineCore;

namespace QuestReader
{
    public class HtmlSafeTemplate<TModel> : RazorEngineTemplateBase
    {
        public new TModel Model { get; set; }

        class RawContent
        {
            public object Value { get; set; }

            public RawContent(object value)
            {
                Value = value;
            }
        }

        public static object Raw(object value)
        {
            return new RawContent(value);
        }

        public override Task WriteAsync(object? obj = null)
        {
            var value = obj is not null and RawContent rawContent
                ? rawContent.Value
                : System.Web.HttpUtility.HtmlEncode(obj);

            return base.WriteAsync(value);
        }

        public override Task WriteAttributeValueAsync(string prefix, int prefixOffset, object? value, int valueOffset, int valueLength, bool isLiteral)
        {
            value = value is RawContent rawContent
                ? rawContent.Value
                : System.Web.HttpUtility.HtmlAttributeEncode(value?.ToString());

            return base.WriteAttributeValueAsync(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral);
        }
    }
}