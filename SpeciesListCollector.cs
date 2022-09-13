using HtmlAgilityPack;
using mige_collector.DAL;

namespace mige_collector
{
    internal class SpeciesListCollector
    {
        public const string BaseUrl = "http://miskolcigombasz.hu";
        public const string SpeciesListUrl = "http://miskolcigombasz.hu/fajlistank.php";

        private MigeContext migeContext;

        private string Text { get; set; } = "";
        public List<Species> Species { get; private set; } = new List<Species>();

        public SpeciesListCollector(MigeContext migeContext)
        {
            this.migeContext = migeContext;
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
                foreach (HtmlNode tableRow in kindTable.ChildNodes.Where(x => x.Name == "tr").Where(x => x != null))
                {
                    if (tableRow.HasClass("Header")) { continue; }
                    if (!tableRow.HasClass("Odd") && !tableRow.HasClass("Even")) { continue; }

                    var a = tableRow.ChildNodes[0].ChildNodes[0];
                    if (a.HasAttributes && a.Attributes.Any(x => x.Name == "id" && x.Value == "goHome")) { continue; }
                    if (!a.HasAttributes) { continue; } // no link, just text

                    Species species;

                    string stringMigeID = a.Attributes["id"].Value.Replace("kLI", "");
                    int parsedMigeID;
                    if (int.TryParse(stringMigeID, out parsedMigeID))
                    {
                        var foundSpecies = migeContext.Species?.FirstOrDefault(x => x.MigeID == parsedMigeID);
                        if (foundSpecies is null)
                        {
                            species = new();
                            species.MigeID = parsedMigeID;
                        }
                        else
                        {
                            species = foundSpecies;
                            species.SpeciesImages = migeContext.Images?.Where(x => x.SpeciesID == species.ID)?.ToList() ?? new();
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Could not parse mushroom MigeID: " + stringMigeID);
                    }                    

                    species.Url = BaseUrl + a.Attributes["href"].Value;
                    species.NameHU = a.InnerHtml;
                    species.NameLatin = tableRow.ChildNodes[2].InnerHtml;
                    species.OldNameHU = tableRow.ChildNodes[4].InnerHtml;
                    species.OldNameLatin = tableRow.ChildNodes[6].InnerHtml;

                    FillSpeciesInfo(species);

                    if (migeContext.Species?.Find(species.ID) is null)
                    {
                        migeContext.Species?.Add(species);
                    }

                    migeContext.SaveChanges();

                    foreach (var image in species.SpeciesImages)
                    {
                        image.SpeciesID = species.ID;
                        if (!migeContext.Images?.Any(x => x.Url == image.Url && x.SpeciesID == species.ID) ?? false)
                        {
                            migeContext.Images?.Add(image);
                        }
                    }
                    
                    migeContext.SaveChanges();
                }
            }
        }

        private void FillSpeciesInfo(Species species)
        {
            Console.WriteLine(species.NameHU);

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

            string lastProperty = "";

            var kindIconsImg = html.DocumentNode.SelectNodes("//p[@class='KindIcon']/span");
            if (kindIconsImg is not null)
            {
                foreach (var kindIconImg in kindIconsImg)
                    if (kindIconsImg is not null)
                    {
                        string edibilityTextCandidate = kindIconImg.InnerText;
                        if (species.EdibilityShortText.Contains(edibilityTextCandidate)) { continue; }


                        if (!edibilityTextCandidate.Contains("ehető", StringComparison.CurrentCultureIgnoreCase) && !edibilityTextCandidate.Contains("mérgező", StringComparison.CurrentCultureIgnoreCase) &&
                            !edibilityTextCandidate.Contains("védett", StringComparison.CurrentCultureIgnoreCase) && !edibilityTextCandidate.Contains("fogyasztható", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // todo should check edibility info only at the end
                            Console.WriteLine("Edibility info missing, this piece of text should be assigned to another property.");
                        }

                        if (species.EdibilityShortText != "") { species.EdibilityShortText += Environment.NewLine; }
                        species.EdibilityShortText += edibilityTextCandidate;
                        lastProperty = nameof(species.EdibilityShortText);
                    }
            }

            foreach (var p in html.DocumentNode.SelectNodes("//p"))
            {
                string trimmedText = p.InnerText.Trim();
                if (trimmedText == "") { continue; }

                if (trimmedText.Contains("Kalap:") || trimmedText.Trim().StartsWith("A kucsma") ||
                    trimmedText.StartsWith("Feji rész:"))
                {
                    species.CapText = trimmedText;
                    lastProperty = nameof(species.CapText);
                }
                else if (trimmedText.Contains("Termőtest párna (sztróma):", StringComparison.InvariantCultureIgnoreCase) || 
                    trimmedText.Contains("Termőtest:") || trimmedText.Contains("Termőtest-párna (sztróma):") ||
                    trimmedText.StartsWith("A termőtest") || 
                    trimmedText.Replace(" ", "").Replace("(", "").Replace("-", "").Replace(")", "").Contains("Sztrómatermőtestpárna:"))
                {
                    if (species.StromaText != "") { species.StromaText += Environment.NewLine; }
                    species.StromaText = trimmedText;
                    lastProperty = nameof(species.StromaText);
                }
                else if (trimmedText.Contains("Mikroszkopikus bélyegek:"))
                {
                    species.MicroscopicText = trimmedText;
                    lastProperty = nameof(species.MicroscopicText);
                }
                else if (trimmedText.Contains("Gleba:"))
                {
                    species.GlebaText = trimmedText;
                    lastProperty = nameof(species.GlebaText);
                }
                else if (trimmedText.Contains("Spórák:") || trimmedText.Contains("Spóra:") ||
                    trimmedText.Contains("Spórapor:"))
                {
                    species.SporesText = trimmedText;
                    lastProperty = nameof(species.SporesText);
                }
                else if (trimmedText.Contains("Lemezek:") || trimmedText.Contains("Tráma, termőréteg:") ||
                    trimmedText.TrimStart().StartsWith("Termőréteg:"))
                {
                    species.GillsText = trimmedText;
                    lastProperty = nameof(species.GillsText);
                }
                else if (trimmedText.Contains("Tönk:") || trimmedText.TrimStart().StartsWith("A tönk") ||
                    trimmedText.TrimStart().StartsWith("Nyélrész:"))
                {
                    species.StalkText = trimmedText;
                    lastProperty = nameof(species.StalkText);
                }
                else if (trimmedText.Contains("Hús:"))
                {
                    species.FleshText = trimmedText;
                    lastProperty = nameof(species.FleshText);
                }
                else if (trimmedText.Contains("Előfordulás:") || trimmedText.Contains("Élőhely:") ||
                    trimmedText.Contains("Termőhely és idő:") || trimmedText.Contains("Termőhely:"))
                {
                    species.PresenceText = trimmedText;
                    lastProperty = nameof(species.PresenceText);
                }
                else if (trimmedText.Contains("Étkezési érték:"))
                {
                    species.EdibilityText = trimmedText;
                    lastProperty = nameof(species.EdibilityText);
                }
                else if (trimmedText.Contains("Veszélyeztetettség"))
                {
                    species.EndangermentText = trimmedText;
                    lastProperty = nameof(species.EndangermentText);
                }
                else if (trimmedText.Contains("Természetvédelmi értéke:"))
                {
                    species.ProtectionValueText = trimmedText;
                    lastProperty = nameof(species.ProtectionValueText);
                }
                else if (trimmedText.Contains("Hasonló fajok:"))
                {
                    species.SimilarSpeciesText = trimmedText;
                    lastProperty = nameof(species.SimilarSpeciesText);
                }
                else if (trimmedText.Contains("Forrás:"))
                {
                    species.SourceText = trimmedText;
                    lastProperty = nameof(species.SourceText);
                }
                else if (trimmedText.Contains("Megjegyzés:") || trimmedText.Contains("Elkülönítő bélyegei:") ||
                    trimmedText.StartsWith("Hasonló fajok:"))
                {
                    if (species.CommentText != "") { species.CommentText += Environment.NewLine; }
                    species.CommentText += trimmedText;
                    lastProperty = nameof(species.CommentText);
                }
                else if (trimmedText.Contains("A termőtest"))
                {
                    if (species.StromaText != "") { species.StromaText += Environment.NewLine; }
                    species.StromaText = trimmedText;
                    lastProperty = nameof(species.StromaText);
                }
                else if (trimmedText.Contains("Syn."))
                {
                    // don't care
                }
                else if (!string.IsNullOrEmpty(lastProperty) && trimmedText.Trim() != "")
                {
                    // add it as new line to last edited field
                    string currentValue = species.GetType().GetProperty(lastProperty).GetValue(species) as string ?? "";
                    if (currentValue.Contains(trimmedText.Trim())) { continue; }

                    if (currentValue != "") { currentValue += Environment.NewLine; }
                    currentValue += trimmedText;

                    species.GetType().GetProperty(lastProperty)?.SetValue(species, currentValue);
                }
                else if (string.IsNullOrEmpty(lastProperty) && trimmedText.Trim() != "" && trimmedText.Trim() != "Vissza a fajlistához")
                {
                    Console.WriteLine("Dont know where to put this");
                }
            }

            // Validity check
            if (species.CapText+species.StromaText == "")
            {
                if (species.EdibilityShortText.Contains(Environment.NewLine))
                {
                    var splitted = species.EdibilityShortText.Split(Environment.NewLine);
                    species.StromaText = splitted[splitted.Length - 1];
                    species.EdibilityShortText = string.Join(Environment.NewLine, splitted.Where((x, i) => i > 0 && i < splitted.Length - 1).ToList());
                }
                else if (species.NameHU.Equals("Gyengeillatú pókhálósgomba*") ||
                    species.NameHU.Equals("Kerek ráspolygomba"))
                {
                    // There's no valid info for these
                }
                else 
                {
                    Console.WriteLine("Both CapText and StromaText is missing");
                }
            }
            else if (species.CapText != "" && species.GillsText == "")
            {
                Console.WriteLine("CapText exists but GillsText is missing");
            }

            var imgNodes = html.DocumentNode.SelectNodes("//div[@class='BT']/div/div/img");
            if (imgNodes is not null)
            {
                foreach (var img in html.DocumentNode.SelectNodes("//div[@class='BT']/div/div/img"))
                {
                    string url = BaseUrl + img.Attributes.FirstOrDefault(x => x.Name == "src")?.Value ?? "";

                    if (!species.SpeciesImages.Any(x => x.Url == url))
                    {
                        species.SpeciesImages.Add(new SpeciesImage() { Url = url });
                    }
                }
            }
        }

    }
}
