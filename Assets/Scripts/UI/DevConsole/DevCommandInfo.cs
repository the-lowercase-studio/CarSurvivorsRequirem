namespace Assets.Scripts.UI.DevConsole
{
    public readonly struct DevCommandInfo
    {
        public string Name { get; }
        public string Syntax { get; }
        public string Description { get; }

        public DevCommandInfo(string name, string syntax, string description)
        {
            Name = name;
            Syntax = syntax;
            Description = description;
        }
    }
}
