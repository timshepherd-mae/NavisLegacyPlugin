namespace NavisLegacyPlugin.Services.Matching
{
    /// <summary>Phase 6.5A3B runtime-only option. Defaults to false.</summary>
    public static class DuplicateGuidMatchOptions
    {
        private static bool _expectDuplicateGuids;
        public static bool ExpectDuplicateGuids
        {
            get { return _expectDuplicateGuids; }
            set { _expectDuplicateGuids = value; }
        }
    }
}
