using System.Text.Json.Serialization;

namespace mige_collector
{
    public class Species
    {
        public int ID { get; set; }
        public int MigeID { get; set; }
        public string Url { get; set; } = "";
        public string NameHU { get; set; } = "";
        public string NameLatin { get; set; } = "";
        public string OldNameHU { get; set; } = "";
        public string OldNameLatin { get; set; } = "";

        [JsonIgnore]
        public string? FullText { get; set; }
        public string EdibilityShortText { get; set; } = "";
        public string StromaText { get; set; } = "";
        public string GlebaText { get; set; } = "";
        public string SporesText { get; set; } = "";
        public string MicroscopicText { get; set; } = "";
        public string CapText { get; set; } = "";
        public string GillsText { get; set; } = "";
        public string StalkText { get; set; } = "";
        public string FleshText { get; set; } = "";
        public string PresenceText { get; set; } = "";
        public string EndangermentText { get; set; } = "";
        public string ProtectionValueText { get; set; } = "";
        public string EdibilityText { get; set; } = "";
        public string SimilarSpeciesText { get; set; } = "";
        public string SourceText { get; set; } = "";
        public string CommentText { get; set; } = "";

        public List<SpeciesImage> SpeciesImages = new List<SpeciesImage>();

        public void CopyFrom(Species other)
        {
            FullText = other.FullText;
            EdibilityShortText = other.EdibilityShortText;
            StromaText = other.StromaText;
            GlebaText = other.GlebaText;
            SporesText = other.SporesText;
            MicroscopicText = other.MicroscopicText;
            CapText = other.CapText;
            GillsText = other.GillsText;
            StalkText = other.StalkText;
            FleshText = other.FleshText;
            PresenceText = other.PresenceText;
            EndangermentText = other.EndangermentText;
            ProtectionValueText = other.ProtectionValueText;
            EdibilityText = other.EdibilityText;
            SimilarSpeciesText = other.SimilarSpeciesText;
            SourceText = other.SourceText;
            CommentText = other.CommentText;

            SpeciesImages = other.SpeciesImages;
        }
    }
}
