using System.Collections.Generic;

namespace athenaeumBot.Models
{
    public class ArtRaw : IWebItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string ArtistName { get; set; }
        public string ArtistUrl { get; set; }
        public string ImageUrl { get; set; }
        public string Copyright { get; set; }
        public Dictionary<string,string> Details { get; set; }
    }
}