using RazorEngineCore;

namespace QuestReader
{
    public class HtmlSafeTemplate : RazorEngineTemplateBase
    {
        class RawContent
        {
            public object Value { get; set; }

            public RawContent(object value)
            {
                Value = value;
            }
        }


        public object Raw(object value)
        {
            return new RawContent(value);
        }

        public override Task WriteAsync(object obj = null)
        {
            object value = obj is RawContent rawContent
                ? rawContent.Value
                : System.Web.HttpUtility.HtmlEncode(obj);

            return base.WriteAsync(value);
        }

        public override Task WriteAttributeValueAsync(string prefix, int prefixOffset, object value, int valueOffset, int valueLength, bool isLiteral)
        {
            value = value is RawContent rawContent
                ? rawContent.Value
                : System.Web.HttpUtility.HtmlAttributeEncode(value?.ToString());

            return base.WriteAttributeValueAsync(prefix, prefixOffset, value, valueOffset, valueLength, isLiteral);
        }
    }
}