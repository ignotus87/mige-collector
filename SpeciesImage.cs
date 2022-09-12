namespace mige_collector
{
    public class SpeciesImage
    {
        public int ID { get; set; }
        public int SpeciesID { get; set; }
        public string Url { get; set; } = "";

        public virtual Species? Species { get; set; }
    }
}
