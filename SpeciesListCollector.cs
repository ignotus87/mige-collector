using HtmlAgilityPack;
using mige_collector.DAL;
using System.Text.Json;

namespace mige_collector
{
    internal class SpeciesListCollector
    {
        public const string BaseUrl = "http://miskolcigombasz.hu";
        public const string SpeciesListUrl = "http://miskolcigombasz.hu/fajlistank.php";
        public const string DataFolder = "c:\\tmp\\MigeData";
        public const string SpeciesFolder = "Species";
        public const string SpeciesImagesFolder = "SpeciesImages";

        private MigeContext migeContext;

        private string Text { get; set; } = "";
        public List<Species> Species { get; private set; } = new List<Species>();
        private string LastUpdatedProperty = "";

        public SpeciesListCollector(MigeContext migeContext)
        {
            this.migeContext = migeContext;
        }

        public void Collect()
        {
            EnsureDataFoldersExist();
            CrawlSpeciesListPage();
            ParseSpeciesList();
        }

        private void EnsureDataFoldersExist()
        {
            Directory.CreateDirectory(Path.Combine(DataFolder, SpeciesFolder));
            Directory.CreateDirectory(Path.Combine(DataFolder, SpeciesImagesFolder));
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
                    SaveSpecies(species);
                }
            }
        }

        private void SaveSpecies(Species species)
        {
            SaveToDatabase(species);
            SaveToJson(species);
        }

        private void SaveToJson(Species species)
        {
            string fileName = Path.Combine(DataFolder, SpeciesFolder, $"{species.ID}.json");
            string jsonString = JsonSerializer.Serialize(species);
            File.WriteAllText(fileName, jsonString);

            foreach (var image in species.SpeciesImages)
            {
                fileName = Path.Combine(DataFolder, SpeciesImagesFolder, $"{image.ID}.json");
                jsonString = JsonSerializer.Serialize(image);
                File.WriteAllText(fileName, jsonString);
            }
        }

        private void SaveToDatabase(Species species)
        {
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

        private void FillSpeciesInfo(Species existingSpecies)
        {
            Console.WriteLine(existingSpecies.NameHU);
            var actualSpecies = new Species();

            string pageText = "";

            using (HttpClient client = new HttpClient())
            {
                var result = client.GetAsync(existingSpecies.Url).GetAwaiter().GetResult();
                if (result is not null && result.Content is not null && result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    pageText = result.Content.ReadAsStringAsync().Result;
                }
            }

            if (string.IsNullOrEmpty(pageText)) { return; }

            HtmlDocument html = new HtmlDocument();
            html.LoadHtml(pageText);

            LastUpdatedProperty = "";

            var kindIconsImg = html.DocumentNode.SelectNodes("//p[@class='KindIcon']/span");
            if (kindIconsImg is not null)
            {
                foreach (var kindIconImg in kindIconsImg)
                    if (kindIconsImg is not null)
                    {
                        string edibilityTextCandidate = kindIconImg.InnerText;
                        if (actualSpecies.EdibilityShortText.Contains(edibilityTextCandidate)) { continue; }


                        if (!edibilityTextCandidate.Contains("ehető", StringComparison.CurrentCultureIgnoreCase) && !edibilityTextCandidate.Contains("mérgező", StringComparison.CurrentCultureIgnoreCase) &&
                            !edibilityTextCandidate.Contains("védett", StringComparison.CurrentCultureIgnoreCase) && !edibilityTextCandidate.Contains("fogyasztható", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // todo should check edibility info only at the end
                            Console.WriteLine("Edibility info missing, this piece of text should be assigned to another property.");
                        }

                        if (actualSpecies.EdibilityShortText != "") { actualSpecies.EdibilityShortText += Environment.NewLine; }
                        actualSpecies.EdibilityShortText += edibilityTextCandidate;
                        LastUpdatedProperty = nameof(actualSpecies.EdibilityShortText);
                    }
            }

            foreach (var p in html.DocumentNode.SelectNodes("//p"))
            {
                string trimmedText = p.InnerText.Trim();
                if (trimmedText == "") { continue; }

                if (trimmedText.Contains("Kalap:") || trimmedText.Trim().StartsWith("A kucsma") ||
                    trimmedText.StartsWith("Feji rész:"))
                {
                    actualSpecies.CapText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.CapText);
                }
                else if (trimmedText.Contains("Termőtest párna (sztróma):", StringComparison.InvariantCultureIgnoreCase) || 
                    trimmedText.Contains("Termőtest:") || trimmedText.Contains("Termőtest-párna (sztróma):") ||
                    trimmedText.StartsWith("A termőtest") || 
                    trimmedText.Replace(" ", "").Replace("(", "").Replace("-", "").Replace(")", "").Contains("Sztrómatermőtestpárna:"))
                {
                    if (actualSpecies.StromaText != "") { actualSpecies.StromaText += Environment.NewLine; }
                    actualSpecies.StromaText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.StromaText);
                }
                else if (trimmedText.Contains("Mikroszkopikus bélyegek:"))
                {
                    actualSpecies.MicroscopicText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.MicroscopicText);
                }
                else if (trimmedText.Contains("Gleba:"))
                {
                    actualSpecies.GlebaText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.GlebaText);
                }
                else if (trimmedText.Contains("Spórák:") || trimmedText.Contains("Spóra:") ||
                    trimmedText.Contains("Spórapor:"))
                {
                    actualSpecies.SporesText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.SporesText);
                }
                else if (trimmedText.Contains("Lemezek:") || trimmedText.Contains("Tráma, termőréteg:") ||
                    trimmedText.TrimStart().StartsWith("Termőréteg:"))
                {
                    actualSpecies.GillsText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.GillsText);
                }
                else if (trimmedText.Contains("Tönk:") || trimmedText.TrimStart().StartsWith("A tönk") ||
                    trimmedText.TrimStart().StartsWith("Nyélrész:"))
                {
                    actualSpecies.StalkText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.StalkText);
                }
                else if (trimmedText.Contains("Hús:"))
                {
                    actualSpecies.FleshText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.FleshText);
                }
                else if (trimmedText.Contains("Előfordulás:") || trimmedText.Contains("Élőhely:") ||
                    trimmedText.Contains("Termőhely és idő:") || trimmedText.Contains("Termőhely:"))
                {
                    actualSpecies.PresenceText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.PresenceText);
                }
                else if (trimmedText.Contains("Étkezési érték:"))
                {
                    actualSpecies.EdibilityText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.EdibilityText);
                }
                else if (trimmedText.Contains("Veszélyeztetettség"))
                {
                    actualSpecies.EndangermentText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.EndangermentText);
                }
                else if (trimmedText.Contains("Természetvédelmi értéke:"))
                {
                    actualSpecies.ProtectionValueText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.ProtectionValueText);
                }
                else if (trimmedText.Contains("Hasonló fajok:"))
                {
                    actualSpecies.SimilarSpeciesText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.SimilarSpeciesText);
                }
                else if (trimmedText.Contains("Forrás:"))
                {
                    actualSpecies.SourceText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.SourceText);
                }
                else if (trimmedText.Contains("Megjegyzés:") || trimmedText.Contains("Elkülönítő bélyegei:") ||
                    trimmedText.StartsWith("Hasonló fajok:"))
                {
                    if (actualSpecies.CommentText != "") { actualSpecies.CommentText += Environment.NewLine; }
                    actualSpecies.CommentText += trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.CommentText);
                }
                else if (trimmedText.Contains("A termőtest"))
                {
                    if (actualSpecies.StromaText != "") { actualSpecies.StromaText += Environment.NewLine; }
                    actualSpecies.StromaText = trimmedText;
                    LastUpdatedProperty = nameof(actualSpecies.StromaText);
                }
                else if (trimmedText.Contains("Syn."))
                {
                    // don't care
                }
                else if (!string.IsNullOrEmpty(LastUpdatedProperty) && trimmedText.Trim() != "")
                {
                    // add it as new line to last edited field
                    string currentValue = actualSpecies.GetType().GetProperty(LastUpdatedProperty).GetValue(actualSpecies) as string ?? "";
                    if (currentValue.Contains(trimmedText.Trim())) { continue; }

                    if (currentValue != "") { currentValue += Environment.NewLine; }
                    currentValue += trimmedText;

                    actualSpecies.GetType().GetProperty(LastUpdatedProperty)?.SetValue(actualSpecies, currentValue);
                }
                else if (string.IsNullOrEmpty(LastUpdatedProperty) && trimmedText.Trim() != "" && trimmedText.Trim() != "Vissza a fajlistához")
                {
                    Console.WriteLine("Dont know where to put this");
                }
            }

            if (!(actualSpecies.FullText?.Equals(pageText, StringComparison.InvariantCultureIgnoreCase) ?? false))
            {
                actualSpecies.FullText = pageText;
            }

            // Validity check
            if (actualSpecies.CapText+actualSpecies.StromaText == "")
            {
                if (actualSpecies.EdibilityShortText.Contains(Environment.NewLine))
                {
                    var splitted = actualSpecies.EdibilityShortText.Split(Environment.NewLine);
                    actualSpecies.StromaText = splitted[splitted.Length - 1];
                    actualSpecies.EdibilityShortText = string.Join(Environment.NewLine, splitted.Where((x, i) => i > 0 && i < splitted.Length - 1).ToList());
                }
                else if (existingSpecies.NameHU.Equals("Gyengeillatú pókhálósgomba*") ||
                    existingSpecies.NameHU.Equals("Kerek ráspolygomba"))
                {
                    // There's no valid info for these
                }
                else 
                {
                    Console.WriteLine("Both CapText and StromaText is missing");
                }
            }
            else if (actualSpecies.CapText != "" && actualSpecies.GillsText == "")
            {
                Console.WriteLine("CapText exists but GillsText is missing");
            }

            var imgNodes = html.DocumentNode.SelectNodes("//div[@class='BT']/div/div/img");
            if (imgNodes is not null)
            {
                foreach (var img in html.DocumentNode.SelectNodes("//div[@class='BT']/div/div/img"))
                {
                    string url = BaseUrl + img.Attributes.FirstOrDefault(x => x.Name == "src")?.Value ?? "";

                    if (!actualSpecies.SpeciesImages.Any(x => x.Url == url))
                    {
                        actualSpecies.SpeciesImages.Add(new SpeciesImage() { Url = url });
                    }
                }
            }

            existingSpecies.CopyFrom(actualSpecies);

        }

    }
}
