using System.Collections.Generic;

namespace athenaeumBot.Models
{
    public class ArtistRaw : IWebItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public Dictionary<string,string> Details { get; set; }

        public List<string> ArtworksCategories { get; set; }
        // public string Description { get; set; }
        // public string ExternalLink { get; set; }
    }
}