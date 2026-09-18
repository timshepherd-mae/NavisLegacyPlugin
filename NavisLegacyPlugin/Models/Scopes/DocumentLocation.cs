namespace NavisLegacyPlugin.Models.Scopes
{
    public abstract class DocumentLocation
    {
        protected DocumentLocation(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName
        {
            get;
            private set;
        }
    }
}
