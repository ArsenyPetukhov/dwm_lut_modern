namespace DwmLutGUI
{
    internal sealed class ResolverModeOption
    {
        public ResolverModeOption(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; }
        public string Label { get; }
    }
}
