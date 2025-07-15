using Microsoft.EntityFrameworkCore;
using TravelThingBackend.Data;
using TravelThingBackend.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace TravelThingBackend.Services
{
    public class FuelPriceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FuelPriceService> _logger;

        public FuelPriceService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<FuelPriceService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(bool Success, decimal Price, string Message, string DebugInfo)> TestSingleCityUpdate(string city, string fuelType)
        {
            _logger.LogInformation("Testing price update for {City} - {FuelType}", city, fuelType);

            try
            {
                // Creăm client cu decompresie automată
                var handler = new HttpClientHandler()
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                var client = new HttpClient(handler);
                
                // Configurare mai robustă pentru a simula un browser real
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "ro-RO,ro;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                client.Timeout = TimeSpan.FromSeconds(45); // Timeout mai mare pentru stabilitate
                
                var url = "https://www.peco-online.ro/index.php";

                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("carburant", fuelType),
                    new KeyValuePair<string, string>("locatie", "Judet"),
                    new KeyValuePair<string, string>("nume_locatie", city)
                };

                // Adăugăm toate rețelele de benzinării
                var networks = new[] { "Petrom", "OMV", "Rompetrol", "Lukoil", "Mol", "Socar", "Gazprom" };
                foreach (var network in networks)
                {
                    formData.Add(new KeyValuePair<string, string>("retele[]", network));
                }

                var content = new FormUrlEncodedContent(formData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                
                var response = await client.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    return (false, 0, $"Request failed with status {response.StatusCode}", "");
                }

                var html = await response.Content.ReadAsStringAsync();
                var htmlLength = html.Length;
                var containsResultate = html.Contains("rezultate");
                var containsJsonNull = html.Contains("var rezultate = JSON.parse('null')");
                
                var debugInfo = $"Response length: {htmlLength}, Contains 'rezultate': {containsResultate}, JSON is null: {containsJsonNull}";
                
                var price = ParsePriceFromHtml(html);
                
                if (price > 0)
                {
                    return (true, price, $"Price found successfully: {price} RON", debugInfo);
                }
                else
                {
                    // Încercăm metoda alternativă
                    var alternativePrice = await TryAlternativeRequest(client, city, fuelType);
                    if (alternativePrice > 0)
                    {
                        return (true, alternativePrice, $"Price found with alternative method: {alternativePrice} RON", debugInfo + " | Used alternative method");
                    }
                    else
                    {
                        return (false, 0, "No valid price found", debugInfo + " | HTML snippet: " + html.Substring(0, Math.Min(500, html.Length)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test failed for {City} - {FuelType}", city, fuelType);
                return (false, 0, ex.Message, ex.StackTrace ?? "");
            }
        }

        public async Task UpdateFuelPricesAsync()
        {
            _logger.LogInformation("Începe actualizarea prețurilor combustibilului");

            // Verifică dacă s-a făcut deja o actualizare astăzi
            var lastUpdate = await _context.FuelPrices
                .OrderByDescending(p => p.LastUpdated)
                .Select(p => p.LastUpdated)
                .FirstOrDefaultAsync();

            if (lastUpdate != null && lastUpdate.Date == DateTime.UtcNow.Date)
            {
                _logger.LogInformation("Prețurile au fost deja actualizate astăzi");
                return;
            }

            var cities = City.AllCities;
            var fuelTypes = new[] { "Benzina_Regular", "Motorina_Regular", "GPL", "Benzina_Premium", "Motorina_Premium" };
            var newPrices = new List<FuelPrice>();
            var totalRequests = cities.Length * fuelTypes.Length;
            var successfulRequests = 0;
            var failedCombinations = new List<string>();

            foreach (var city in cities)
            {
                try
                {
                    _logger.LogInformation("Încep procesarea pentru orașul {City}", city);
                    // Creăm client cu decompresie automată
                    var handler = new HttpClientHandler()
                    {
                        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                    };
                    var client = new HttpClient(handler);
                    
                    // Configurare mai robustă pentru a simula un browser real
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
                    client.DefaultRequestHeaders.Add("Accept-Language", "ro-RO,ro;q=0.9,en-US;q=0.8,en;q=0.7");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                    client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    var url = "https://www.peco-online.ro/index.php";

                    foreach (var fuelType in fuelTypes)
                    {
                        _logger.LogInformation("Procesez {FuelType} pentru {City}", fuelType, city);
                        
                        // Delay între requests pentru a evita rate limiting
                        await Task.Delay(1500); // 1.5 secunde între fiecare request
                        
                        // Folosim List<KeyValuePair> în loc de Dictionary pentru a permite chei duplicate
                        var formData = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("carburant", fuelType),
                            new KeyValuePair<string, string>("locatie", "Judet"),
                            new KeyValuePair<string, string>("nume_locatie", city),
                            new KeyValuePair<string, string>("Submit", "Cauta")
                        };

                        // Adăugăm toate rețelele de benzinării
                        var networks = new[] { "Petrom", "OMV", "Rompetrol", "Lukoil", "Mol", "Socar", "Gazprom" };
                        foreach (var network in networks)
                        {
                            formData.Add(new KeyValuePair<string, string>("retele[]", network));
                        }

                        var content = new FormUrlEncodedContent(formData);
                        
                        // Adăugăm header pentru Content-Type
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                        
                        var response = await client.PostAsync(url, content);
                        
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Request failed for {City} - {FuelType}. Status: {StatusCode}", city, fuelType, response.StatusCode);
                            continue;
                        }
                        
                        var html = await response.Content.ReadAsStringAsync();
                        
                        // Logging îmbunătățit pentru debugging
                        _logger.LogInformation("Response received for {City} - {FuelType}. Length: {Length}", city, fuelType, html.Length);
                        
                        // Căutăm indicii că răspunsul conține date utile
                        if (html.Contains("var rezultate = JSON.parse('null')") || !html.Contains("rezultate"))
                        {
                            _logger.LogWarning("Response for {City} - {FuelType} appears to be empty or invalid", city, fuelType);
                            
                            // Încercăm o abordare alternativă - request direct cu parametrii în URL
                            var alternativePrice = await TryAlternativeRequest(client, city, fuelType);
                            if (alternativePrice > 0)
                            {
                                var fuelPrice = new FuelPrice
                                {
                                    City = city,
                                    FuelType = MapFuelType(fuelType),
                                    Price = alternativePrice,
                                    LastUpdated = DateTime.UtcNow.AddHours(3)
                                };
                                newPrices.Add(fuelPrice);
                                successfulRequests++;
                                _logger.LogInformation("Preț găsit cu metoda alternativă pentru {City} - {FuelType}: {Price}", city, fuelType, alternativePrice);
                            }
                            continue;
                        }

                        // Parse HTML response to get price
                        var price = ParsePriceFromHtml(html);
                        _logger.LogInformation("Preț găsit pentru {City} - {FuelType}: {Price}", city, fuelType, price);
                        
                        if (price > 0)
                        {
                            var fuelPrice = new FuelPrice
                            {
                                City = city,
                                FuelType = MapFuelType(fuelType),
                                Price = price,
                                LastUpdated = DateTime.UtcNow.AddHours(3)
                            };
                            newPrices.Add(fuelPrice);
                            successfulRequests++;
                            _logger.LogInformation("✅ Preț adăugat cu succes: {City} - {FuelType}: {Price} RON", city, fuelType, price);
                        }
                        else
                        {
                            var combination = $"{city}-{fuelType}";
                            failedCombinations.Add(combination);
                            _logger.LogWarning("❌ Nu s-a putut obține prețul pentru {City} - {FuelType}", city, fuelType);
                            
                            // Încercăm o a doua oară cu delay mai mare
                            _logger.LogInformation("🔄 Retry pentru {City} - {FuelType} cu delay mai mare...", city, fuelType);
                            await Task.Delay(3000); // 3 secunde delay
                            
                            try 
                            {
                                var retryPrice = await TryAlternativeRequest(client, city, fuelType);
                                if (retryPrice > 0)
                                {
                                    var fuelPrice = new FuelPrice
                                    {
                                        City = city,
                                        FuelType = MapFuelType(fuelType),
                                        Price = retryPrice,
                                        LastUpdated = DateTime.UtcNow.AddHours(3)
                                    };
                                    newPrices.Add(fuelPrice);
                                    successfulRequests++;
                                    failedCombinations.Remove(combination);
                                    _logger.LogInformation("✅ RETRY SUCCESS: {City} - {FuelType}: {Price} RON", city, fuelType, retryPrice);
                                }
                            }
                            catch (Exception retryEx)
                            {
                                _logger.LogError(retryEx, "Retry failed for {City} - {FuelType}", city, fuelType);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching prices for {City}", city);
                }
            }

            // Actualizăm baza de date dacă am obținut cel puțin 50% din prețuri
            if (successfulRequests >= totalRequests * 0.5 && newPrices.Any())
            {
                // Șterge toate prețurile vechi
                _context.FuelPrices.RemoveRange(_context.FuelPrices);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Prețurile vechi au fost șterse");

                // Adaugă prețurile noi
                await _context.FuelPrices.AddRangeAsync(newPrices);
                await _context.SaveChangesAsync();
                var successRate = Math.Round((double)successfulRequests / totalRequests * 100, 1);
                _logger.LogInformation("📊 ACTUALIZARE COMPLETĂ: {Successful}/{Total} prețuri actualizate cu succes ({SuccessRate}%)", 
                    successfulRequests, totalRequests, successRate);
                
                if (failedCombinations.Any())
                {
                    _logger.LogWarning("❌ Combinații eșuate: {FailedCombinations}", string.Join(", ", failedCombinations));
                }
                else
                {
                    _logger.LogInformation("🎉 Toate prețurile au fost actualizate cu succes!");
                }
            }
            else
            {
                _logger.LogWarning("Nu s-au putut obține suficiente prețuri. {Successful}/{Total} prețuri actualizate cu succes.", 
                    successfulRequests, totalRequests);
                throw new Exception($"Nu s-au putut obține suficiente prețuri. {successfulRequests}/{totalRequests} prețuri actualizate cu succes.");
            }
        }

        private decimal ParsePriceFromHtml(string html)
        {
            // Încercăm mai întâi să parsam formatul nou JSON
            var jsonPrice = ParseJsonPrices(html);
            if (jsonPrice > 0)
            {
                _logger.LogInformation("Preț găsit din JSON: {Price}", jsonPrice);
                return jsonPrice;
            }

            // Fallback la metoda veche pentru HTML
            return ParseLegacyHtmlFormat(html);
        }

        private decimal ParseJsonPrices(string html)
        {
            try
            {
                // Căutăm mai multe pattern-uri pentru rezultate JSON
                var patterns = new[]
                {
                    @"var rezultate = JSON\.parse\('(.+?)'\);",
                    @"rezultate = JSON\.parse\('(.+?)'\)",
                    @"""rezultate"":\s*(\[.+?\])",
                    @"var\s+data\s*=\s*JSON\.parse\('(.+?)'\)",
                    @"var\s+results\s*=\s*(\[.+?\])"
                };

                foreach (var pattern in patterns)
                {
                    var jsonMatch = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    
                    if (jsonMatch.Success && jsonMatch.Groups[1].Value != "null" && !string.IsNullOrEmpty(jsonMatch.Groups[1].Value))
                    {
                        var jsonString = jsonMatch.Groups[1].Value;
                        
                        // Decodificăm escape-urile din JSON
                        jsonString = jsonString.Replace("\\/", "/").Replace("\\\"", "\"").Replace("\\'", "'");
                        
                        _logger.LogInformation("Found JSON data with pattern: {Pattern}. Data: {JsonString}", pattern, jsonString.Substring(0, Math.Min(200, jsonString.Length)));
                        
                        var prices = ExtractPricesFromJson(jsonString);
                        if (prices.Any())
                        {
                            var minPrice = prices.Min();
                            _logger.LogInformation("Cel mai mic preț din JSON: {Price}", minPrice);
                            return minPrice;
                        }
                    }
                }
                
                // Căutăm și alte posibile structuri de date în pagină
                var scriptTags = Regex.Matches(html, @"<script[^>]*>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match scriptMatch in scriptTags)
                {
                    var scriptContent = scriptMatch.Groups[1].Value;
                    if (scriptContent.Contains("pret") || scriptContent.Contains("price") || scriptContent.Contains("rezultate"))
                    {
                        _logger.LogInformation("Found script with potential price data: {Script}", scriptContent.Substring(0, Math.Min(300, scriptContent.Length)));
                        
                        // Căutăm prețuri directe în script
                        var priceMatches = Regex.Matches(scriptContent, @"\b(\d+[,.]?\d*)\s*(?:RON|lei)\b", RegexOptions.IgnoreCase);
                        foreach (Match priceMatch in priceMatches)
                        {
                            var priceString = priceMatch.Groups[1].Value;
                            var price = ParsePriceString(priceString);
                            if (IsValidPriceString(priceString))
                            {
                                _logger.LogInformation("Found price in script: {Price}", price);
                                return price;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON prices in FuelPriceService");
            }

            return 0;
        }

        private List<decimal> ExtractPricesFromJson(string jsonString)
        {
            var prices = new List<decimal>();
            var petromPrices = new List<decimal>();
            var otherNetworkPrices = new List<decimal>();
            
            try
            {
                var jsonDocument = JsonDocument.Parse(jsonString);
                
                if (jsonDocument.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in jsonDocument.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 6)
                        {
                            var networkName = item[0].GetString() ?? "";
                            var priceElement = item[5]; // prețul este pe poziția 5
                            decimal price = 0;
                            
                            // Prețul poate fi Number sau String
                            if (priceElement.ValueKind == JsonValueKind.Number)
                            {
                                price = priceElement.GetDecimal();
                                _logger.LogInformation("Găsit preț valid de la {Network} (number): {Price}", networkName, price);
                            }
                            else if (priceElement.ValueKind == JsonValueKind.String)
                            {
                                var priceString = priceElement.GetString();
                                if (!string.IsNullOrEmpty(priceString) && IsValidPriceString(priceString))
                                {
                                    price = ParsePriceString(priceString);
                                    _logger.LogInformation("Găsit preț valid de la {Network} (string): {Price}", networkName, price);
                                }
                            }
                            
                            if (price > 0)
                            {
                                prices.Add(price);
                                
                                // Trackăm prețurile de la Petrom separat
                                if (networkName.Equals("Petrom", StringComparison.OrdinalIgnoreCase))
                                {
                                    petromPrices.Add(price);
                                }
                                else
                                {
                                    otherNetworkPrices.Add(price);
                                }
                            }
                        }
                        else if (item.ValueKind == JsonValueKind.Object)
                        {
                            // Încercăm să găsim proprietăți care ar putea conține prețuri
                            foreach (var property in item.EnumerateObject())
                            {
                                if (property.Name.ToLower().Contains("pret") || property.Name.ToLower().Contains("price"))
                                {
                                    var priceString = property.Value.GetString();
                                    if (!string.IsNullOrEmpty(priceString) && IsValidPriceString(priceString))
                                    {
                                        var price = ParsePriceString(priceString);
                                        if (price > 0)
                                        {
                                            prices.Add(price);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting prices from JSON");
            }
            
            // Logging statistici pentru Petrom vs alte rețele
            if (prices.Any())
            {
                var minPrice = prices.Min();
                var maxPrice = prices.Max();
                _logger.LogInformation("📈 Statistici prețuri găsite: Total={Total}, Petrom={PetromCount}, Alte rețele={OtherCount}", 
                    prices.Count, petromPrices.Count, otherNetworkPrices.Count);
                _logger.LogInformation("💰 Cel mai mic preț: {MinPrice} RON, Cel mai mare preț: {MaxPrice} RON", minPrice, maxPrice);
                
                if (petromPrices.Any() && otherNetworkPrices.Any())
                {
                    var petromAvg = Math.Round(petromPrices.Average(), 3);
                    var otherAvg = Math.Round(otherNetworkPrices.Average(), 3);
                    _logger.LogInformation("⛽ Petrom medie: {PetromAvg} RON | Alte rețele medie: {OtherAvg} RON", petromAvg, otherAvg);
                }
            }
            
            return prices;
        }

        private decimal ParseLegacyHtmlFormat(string html)
        {
            try
            {
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(html);

                // Căutăm toate prețurile din rezultate - metoda veche
                var priceNodes = doc.DocumentNode.SelectNodes("//h5[contains(@class, 'pret')]/strong");
                if (priceNodes != null && priceNodes.Any())
                {
                    // Luăm cel mai mic preț (cel mai avantajos)
                    decimal minPrice = decimal.MaxValue;
                    foreach (var node in priceNodes)
                    {
                        var priceText = node.InnerText.Trim();
                        _logger.LogInformation("Text preț găsit: {PriceText}", priceText);
                        
                        if (IsValidPriceString(priceText))
                        {
                            var price = ParsePriceString(priceText);
                            _logger.LogInformation("Preț parsat: {Price}", price);
                            if (price > 0 && price < minPrice)
                            {
                                minPrice = price;
                            }
                        }
                    }

                    if (minPrice != decimal.MaxValue)
                    {
                        _logger.LogInformation("Cel mai mic preț găsit: {Price}", minPrice);
                        return minPrice;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing legacy HTML format");
            }
            
            _logger.LogWarning("Nu s-au găsit prețuri în rezultate HTML");
            return 0;
        }

        private bool IsValidPriceString(string priceText)
        {
            if (string.IsNullOrEmpty(priceText)) return false;
            
            var price = ParsePriceString(priceText);
            return price > 3 && price < 20; // Preț rezonabil pentru combustibil
        }

        private decimal ParsePriceString(string priceText)
        {
            if (string.IsNullOrEmpty(priceText)) return 0;
            
            // Înlocuim virgula cu punct pentru parsing
            var cleanPrice = priceText.Replace(",", ".").Trim();
            
            if (decimal.TryParse(cleanPrice, out var price))
            {
                return price;
            }
            
            return 0;
        }

        private async Task<decimal> TryAlternativeRequest(HttpClient client, string city, string fuelType)
        {
            try
            {
                // Încercăm cu metoda GET și parametrii în URL
                var queryParams = $"?carburant={fuelType}&locatie=Judet&nume_locatie={Uri.EscapeDataString(city)}";
                var alternativeUrl = $"https://www.peco-online.ro/index.php{queryParams}";
                
                _logger.LogInformation("Trying alternative request for {City} - {FuelType}: {Url}", city, fuelType, alternativeUrl);
                
                var response = await client.GetAsync(alternativeUrl);
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    var price = ParsePriceFromHtml(html);
                    if (price > 0)
                    {
                        return price;
                    }
                }
                
                // Încercăm să simulam o sesiune browser mai realistă
                await Task.Delay(1000); // Pauză pentru a nu suprasolicita serverul
                
                // Încercăm cu o altă combinație de headere
                using var sessionClient = _httpClientFactory.CreateClient();
                sessionClient.DefaultRequestHeaders.Clear();
                sessionClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0");
                sessionClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                sessionClient.DefaultRequestHeaders.Add("Accept-Language", "ro-RO,ro;q=0.8,en-US;q=0.5,en;q=0.3");
                sessionClient.DefaultRequestHeaders.Add("Referer", "https://www.peco-online.ro/");
                
                // Simulăm comportamentul unui utilizator real - mai întâi accesăm pagina principală
                var mainPageResponse = await sessionClient.GetAsync("https://www.peco-online.ro/index.php");
                if (mainPageResponse.IsSuccessStatusCode)
                {
                    // Apoi facem POST-ul cu datele
                    var formData = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("carburant", fuelType),
                        new KeyValuePair<string, string>("locatie", "Judet"),
                        new KeyValuePair<string, string>("nume_locatie", city),
                        new KeyValuePair<string, string>("retele[]", "Petrom"),
                        new KeyValuePair<string, string>("retele[]", "OMV"),
                        new KeyValuePair<string, string>("retele[]", "Rompetrol")
                    };

                    var content = new FormUrlEncodedContent(formData);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    
                    var postResponse = await sessionClient.PostAsync("https://www.peco-online.ro/index.php", content);
                    if (postResponse.IsSuccessStatusCode)
                    {
                        var html = await postResponse.Content.ReadAsStringAsync();
                        return ParsePriceFromHtml(html);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alternative request failed for {City} - {FuelType}", city, fuelType);
            }
            
            return 0;
        }

        private string MapFuelType(string pecoFuelType)
        {
            return pecoFuelType switch
            {
                "Benzina_Regular" => "Benzina Standard",
                "Motorina_Regular" => "Motorina Standard",
                "GPL" => "GPL",
                "Benzina_Premium" => "Benzina Superioara",
                "Motorina_Premium" => "Motorina Premium",
                _ => pecoFuelType
            };
        }
    }
} 