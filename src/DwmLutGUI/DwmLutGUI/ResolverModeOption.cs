namespace DwmLutGUI
{
    internal sealed class ResolverModeOption
    {
        public ResolverModeOption(string value, string label, string detail)
        {
            Value = value;
            Label = label;
            Detail = detail;
        }

        public string Value { get; }
        public string Label { get; }
        public string Detail { get; }
    }
}
