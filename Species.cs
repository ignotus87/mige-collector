namespace mige_collector
{
    internal class Species
    {
        public string Url { get; set; } = "";
        public string NameHU { get; set; } = "";
        public string NameLatin { get; set; } = "";
        public string OldNameHU { get; set; } = "";
        public string OldNameLatin { get; set; } = "";
        public string EdibilityShortText { get; set; } = "";
        public string CapText { get; set; } = "";
        public string GillsText { get; set; } = "";
        public string StalkText { get; set; } = "";
        public string FleshText { get; set; } = "";
        public string PresenceText { get; set; } = "";
        public string EdibilityText { get; set; } = "";
        public string SourceText { get; set; } = "";

        public List<SpeciesImage> SpeciesImages = new List<SpeciesImage>();
    }
}
