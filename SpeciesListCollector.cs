using HtmlAgilityPack;

namespace mige_collector
{
    internal class SpeciesListCollector
    {
        public const string BaseUrl = "http://miskolcigombasz.hu";
        public const string SpeciesListUrl = "http://miskolcigombasz.hu/fajlistank.php";

        private string Text { get; set; } = "";
        public List<Species> Species { get; private set; } = new List<Species>();

        public SpeciesListCollector()
        {
        }

        public void Collect()
        {
            CrawlSpeciesListPage();
            ParseSpeciesList();
        }

        private void CrawlSpeciesListPage()
        {
            using (HttpClient client = new HttpClient())
            {
                var result = client.GetAsync(SpeciesListUrl).GetAwaiter().GetResult();
                if (result is not null && result.Content is not null && result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Text = result.Content.ReadAsStringAsync().Result;
                }
            }
        }

        private void ParseSpeciesList()
        {
            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(Text);

            var kindTables = html.DocumentNode.SelectNodes("//table[@class='KindTable']");

            foreach (HtmlNode kindTable in kindTables)
            {
                foreach (HtmlNode tableRow in kindTable.SelectNodes("//tr"))
                {
                    if (tableRow.HasClass("Header")) { continue; }
                    if (!tableRow.HasClass("Odd") && !tableRow.HasClass("Even")) { continue; }

                    var a = tableRow.ChildNodes[0].ChildNodes[0];
                    if (a.HasAttributes && a.Attributes.Any(x => x.Name == "id" && x.Value == "goHome")) { continue; }

                    Species species = new();

                    species.Url = BaseUrl + a.Attributes["href"].Value;
                    species.NameHU = a.InnerHtml;
                    species.NameLatin = tableRow.ChildNodes[2].InnerHtml;
                    species.OldNameHU = tableRow.ChildNodes[4].InnerHtml;
                    species.OldNameLatin = tableRow.ChildNodes[6].InnerHtml;

                    FillSpeciesInfo(species);
                }
            }
        }

        private void FillSpeciesInfo(Species species)
        {
            string pageText = "";

            using (HttpClient client = new HttpClient())
            {
                var result = client.GetAsync(species.Url).GetAwaiter().GetResult();
                if (result is not null && result.Content is not null && result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    pageText = result.Content.ReadAsStringAsync().Result;
                }
            }

            if (string.IsNullOrEmpty(pageText)) { return; }

            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(pageText);

            var kindIconPImg = html.DocumentNode.SelectSingleNode("//p[@class='KindIcon']/span");
            species.EdibilityShortText = kindIconPImg.InnerText;

            foreach (var p in html.DocumentNode.SelectNodes("//p"))
            {
                if (p.InnerText.Contains("Kalap:"))
                {
                    species.CapText = p.InnerText;
                }
                else if (p.InnerText.Contains("Lemezek:"))
                {
                    species.GillsText = p.InnerText;
                }
                else if (p.InnerText.Contains("Tönk:"))
                {
                    species.StalkText = p.InnerText;
                }
                else if (p.InnerText.Contains("Hús:"))
                {
                    species.FleshText = p.InnerText;
                }
                else if (p.InnerText.Contains("Előfordulás:"))
                {
                    species.PresenceText = p.InnerText;
                }
                else if (p.InnerText.Contains("Étkezési érték:"))
                {
                    species.EdibilityText = p.InnerText;
                }
                else if (p.InnerText.Contains("Forrás:"))
                {
                    species.SourceText = p.InnerText;
                }
            }

            foreach (var img in html.DocumentNode.SelectNodes("//div[@class='BT']/div/div/img"))
            {
                species.SpeciesImages.Add(new SpeciesImage() { Url = BaseUrl + img.Attributes.FirstOrDefault(x => x.Name == "src")?.Value ?? "" });
            }

        }
    }
}
