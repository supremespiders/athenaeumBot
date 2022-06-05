using athenaeumBot.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using athenaeumBot.Extensions;
using ExcelHelperExe;
using Newtonsoft.Json;

namespace athenaeumBot.Services
{
    public class Scraper
    {
        private HttpClient _client;
        private int _threads;

        public Scraper(int threads)
        {
            _threads = threads;
            _client = new HttpClient(new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
        }

        async Task GetArtists()
        {
            var doc = await _client.GetHtml("http://www.the-athenaeum.org/art/counts.php?s=au&m=a").ToDoc();
            var links = doc.DocumentNode.SelectNodes("//a[contains(@href,'/people/detail.php?')]").Select(x => "http://www.the-athenaeum.org" + x.GetAttributeValue("href", ""));
            File.WriteAllLines("artistLinks", links);
        }

        async Task ScrapeArtists(CancellationToken ct)
        {
            var links = File.ReadAllLines("artistLinks");
            // var artists = await links.Scrape(x => GetArtistDetails(x, ct));
            var artists = await links.ScrapeParallel(_threads, x => GetArtistDetails(x, ct));
            artists.Save(nameof(ArtistRaw));
        }

        async Task<ArtistRaw> GetArtistDetails(string url, CancellationToken ct)
        {
            var doc = await _client.GetHtml(url, ct: ct).ToDoc();
            var title = doc.NodeText("//*[@id='title']");
            var subTitle = doc.NodeText("//div[@class='subtitle']");
            var trs = doc.DocumentNode.SelectNodes("//div[@id='bio']//tr");
            var details = new Dictionary<string, string>();
            foreach (var tr in trs)
            {
                var td = tr.SelectNodes("./td");
                details.Add(td[0].InnerText.Replace(":", ""), td[1].InnerText);
            }

            var artworksCategories = doc.DocumentNode.SelectNodes("//a[contains(@href,'/art/list.php')]")?.Select(x => "http://www.the-athenaeum.org" + x.GetAttributeValue("href", "")).ToList();

            return new ArtistRaw
            {
                Title = title,
                Url = url,
                Details = details,
                SubTitle = subTitle,
                ArtworksCategories = artworksCategories
            };
        }

        async Task GetArtsUrlsMain(CancellationToken ct)
        {
            var artists = "ArtistRaw".Load<ArtistRaw>();
            var inputs = artists.Where(x => x.ArtworksCategories != null).SelectMany(x => x.ArtworksCategories).ToList();
            var urls = await inputs.ScrapeParallel(_threads, x => GetArtsUrls(x, ct));
            //var urls = await GetArtsUrls(artists.First().ArtworksCategories.First(),ct);
        }

        async Task<List<ArtLink>> GetArtsUrls(string url, CancellationToken ct)
        {
            //url = "http://www.the-athenaeum.org/art/list.php?m=o&s=du&oid=1.&f=a&fa=4150";
            var links = new List<ArtLink>();
            var page = 1;
            var totalPages = 1;
            do
            {
                var doc = await _client.GetHtml($"{url}&p={page}", ct: ct).ToDoc();
                var l = doc.DocumentNode.SelectNodes("//div[@class='list_title']/a")?.Select(x => "http://www.the-athenaeum.org" + x.GetAttributeValue("href", "")).ToList();
                if (l == null && doc.DocumentNode.SelectSingleNode("//a[@title='scholar']") != null)
                {
                    links.Add(new ArtLink { Url = url, ArtUrl = url });
                    return links;
                }

                foreach (var x in l)
                {
                    links.Add(new ArtLink { Url = url, ArtUrl = x });
                }

                //a[@title='scholar']
                if (page == 1)
                {
                    var paging = doc.DocumentNode.SelectNodes("//div[@id='linkbar']/following-sibling::div[1]//a");
                    if (paging == null) return links;
                    var lastPageUrl = paging.Last().GetAttributeValue("href", "");
                    totalPages = int.Parse(lastPageUrl.Substring(lastPageUrl.IndexOf("&p=", StringComparison.Ordinal) + 3));
                }

                if (page == totalPages) return links;
                page++;
            } while (true);
        }

        async Task GetArts(CancellationToken ct)
        {
            // var urls = nameof(ArtLink).Load<ArtLink>();
            // foreach (var artLink in urls)
            // {
            //     artLink.ArtUrl = artLink.ArtUrl.Replace("hhttp", "http");
            // }
            // urls.Save(nameof(ArtLink));
            // var urls = nameof(ArtLink).Load<ArtLink>().Select(x => x.ArtUrl).ToHashSet().ToList();

            // var o = File.ReadAllText("ArtRaw");
            // //var os = o.Substring(121445450, 11);
            // var t = o.Substring(0, o.LastIndexOf(",{\"Url\":\"http://www.the-athenaeum.org/art/detail.php?ID=84228\"", StringComparison.Ordinal))+"]";
            // File.WriteAllText("ArtRaw2",t);
            // var ob = "ArtRaw2".Load<ArtistRaw>();

            var urls = "artUrls".Load<string>();
            var arts = await urls.ToList().ScrapeParallel(_threads, x => GetArtDetails(x, ct));
        }

        async Task ListAllArtsFields()
        {
            var arts = nameof(ArtRaw).Load<ArtRaw>();
            var dic = new Dictionary<string, int>();
            foreach (var artRaw in arts)
            {
                foreach (var d in artRaw.Details)
                {
                    if (!dic.ContainsKey(d.Key))
                        dic.Add(d.Key, 1);
                    else
                        dic[d.Key]++;
                }
            }

            foreach (var i in dic)
            {
                Debug.WriteLine($"{i.Key} => {i.Value}");
            }
        }

        async Task ListAllArtistsFields()
        {
            var arts = nameof(ArtistRaw).Load<ArtistRaw>();
            var dic = new Dictionary<string, int>();
            foreach (var artRaw in arts)
            {
                foreach (var d in artRaw.Details)
                {
                    if (!dic.ContainsKey(d.Key))
                        dic.Add(d.Key, 1);
                    else
                        dic[d.Key]++;
                }
            }

            foreach (var i in dic)
            {
                Debug.WriteLine($"{i.Key} => {i.Value}");
            }
        }

        async Task<ArtRaw> GetArtDetails(string url, CancellationToken ct)
        {
            var doc = await _client.GetHtml(url, ct: ct).ToDoc();
            var title = doc.NodeText("//*[@id='title']");
            var artistName = doc.NodeText("//div[@class='subtitle']/a");
            var artistLink = doc.DocumentNode.SelectSingleNode("//div[@class='subtitle']/a").GetAttributeValue("href", "").Replace("../", "http://www.the-athenaeum.org/");
            var detailsNode = doc.DocumentNode.SelectNodes("//div[@id='generalInfo']//tr").ToDictionary(
                x => x.SelectSingleNode("./td[1]").InnerText.Replace(":", "").Clean(),
                y => y.SelectSingleNode("./td[2]").InnerText.Clean());
            var img = doc.DocumentNode.SelectSingleNode("//td[@align='center']/a")?.GetAttributeValue("href", "").Insert(0, "http://www.the-athenaeum.org/art/");
            var publicDomain = doc.DocumentNode.SelectSingleNode("//strong[text()='PUBLIC DOMAIN']") != null;
            if (!publicDomain)
                Console.WriteLine();
            var copyright = "Public Domain";
            var copyrightNode = doc.NodeText("//strong[text()='Artwork copyright']/following-sibling::div/strong");
            return new ArtRaw
            {
                Url = url,
                Title = title,
                ArtistName = artistName,
                ArtistUrl = artistLink,
                Details = detailsNode,
                ImageUrl = img,
                Copyright = copyrightNode
            };
        }

        string GetLocalPath(string prefix, Dictionary<string, string> dic)
        {
            for (var i = 1; i < 1000; i++)
            {
                var finalName = $"imgs/{prefix + i}.jpg";
                if (!dic.ContainsKey(finalName))
                    return finalName;
            }

            throw new KnownException($"Failed to find a unique name till 1000");
        }

        async Task WriteArts()
        {
            var artRaws = nameof(ArtRaw).Load<ArtRaw>();
            var arts = new List<Artwork>();
            var dic = new Dictionary<string, string>();
            foreach (var x in artRaws)
            {
                x.Title = WebUtility.HtmlDecode(x.Title);
                x.ArtistName = WebUtility.HtmlDecode(x.ArtistName);
                var localImg = $"{x.ArtistName.ReplaceInvalidChars()}_{x.Title.ReplaceInvalidChars()}_";
                if (localImg.Length > 240) localImg = $"{x.ArtistName.ReplaceInvalidChars()}_";
                if (localImg.Length > 240) localImg = $"_";
                localImg = GetLocalPath(localImg, dic);
                dic.Add(localImg, x.ImageUrl);
                arts.Add(new Artwork
                {
                    Url = x.Url,
                    Title = x.Title,
                    Copyright = x.Copyright,
                    ArtistName = x.ArtistName,
                    ArtistUrl = x.ArtistUrl,
                    ImageUrl = x.ImageUrl,
                    ImageLocalPath = x.ImageUrl == null ? null : localImg,
                    OwnerLocation = x.Details.GetValueOrDefault("Owner/Location"),
                    Dates = x.Details.GetValueOrDefault("Dates"),
                    ArtistAge = x.Details.GetValueOrDefault("Artist age"),
                    Dimensions = x.Details.GetValueOrDefault("Dimensions"),
                    Medium = x.Details.GetValueOrDefault("Medium"),
                    EnteredBy = x.Details.GetValueOrDefault("Entered by"),
                });
            }

            File.WriteAllText("imgUrls", JsonConvert.SerializeObject(dic));
            await arts.SaveToExcel("artworks.xlsx");
        }

        async Task WriteArtists()
        {
            var artistsRaw = nameof(ArtistRaw).Load<ArtistRaw>();
            var artists = new List<Artist>();
            foreach (var x in artistsRaw)
            {
                artists.Add(new Artist()
                {
                    Url = x.Url,
                    Name = WebUtility.HtmlDecode(x.Title),
                    Dates = x.Details["Dates"],
                    Sex = x.Details["Sex"],
                    Nationality = x.Details["Nationality"],
                });
            }

            await artists.SaveToExcel("artists.xlsx");
        }

        async Task DownloadAllImages(CancellationToken ct)
        {
            Directory.CreateDirectory("imgs");
            Notifier.Display("Parsing images links...");
            var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("imgUrls"));

            var links = new Dictionary<string, string>();
            foreach (var v in dic)
            {
                if (v.Value == null) continue;
                if (File.Exists(v.Key)) continue;
                links.Add(v.Key, v.Value);
            }
            //var y = dic.Keys.Count(x => x.Length > 240);


            await links.ToList().Work(_threads, x => _client.DownloadFile(x.Value, x.Key, ct));
        }


        public async Task MainWork(CancellationToken ct)
        {
            //await GetArtists();
            // await ScrapeArtists(ct);
            // await GetArtsUrlsMain(ct);
            //await GetArts(ct);
            //await ListAllArtsFields();
            //await ListAllArtistsFields();
            //await WriteArts();
            //await WriteArtists();
            await DownloadAllImages(ct);
        }
    }
}