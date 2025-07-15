using Microsoft.AspNetCore.Mvc;
using HtmlAgilityPack;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Linq;

namespace TravelThingBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarburantController : ControllerBase
    {
        private readonly ILogger<CarburantController> _logger;

        public CarburantController(ILogger<CarburantController> logger)
        {
            _logger = logger;
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestScraping([FromQuery] string nume_locatie = "Cluj")
        {
            _logger.LogInformation("Testing scraping for location: {Location}", nume_locatie);
            
            try
            {
                var rezultate = await GetPreturiCarburantAsync("Judet", "Benzina_Regular", nume_locatie, "");
                return Ok(new 
                { 
                    success = true,
                    location = nume_locatie,
                    results = rezultate.Take(5), // Primele 5 rezultate pentru test
                    total_count = rezultate.Count,
                    message = "Scraping test completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test scraping failed");
                return Ok(new 
                { 
                    success = false,
                    error = ex.Message,
                    message = "Scraping test failed"
                });
            }
        }

        [HttpGet("pret")]
        public async Task<IActionResult> GetPret([FromQuery] string locatie, [FromQuery] string tip, [FromQuery] string nume_locatie, [FromQuery] string retea = "Petrom")
        {
            var preturi = await GetPreturiCarburantAsync(locatie, tip, nume_locatie, retea);
            var pretMinim = preturi.Where(p => !string.IsNullOrEmpty(p.Pret) && p.Pret != "N/A")
                                   .OrderBy(p => ParsePrice(p.Pret))
                                   .FirstOrDefault();
            
            var pret = !string.IsNullOrEmpty(pretMinim.Pret) ? pretMinim.Pret : "N/A";
            return Ok(new { pret });
        }

        [HttpGet("preturi")]
        public async Task<IActionResult> GetPreturi([FromQuery] string locatie, [FromQuery] string tip, [FromQuery] string nume_locatie, [FromQuery] string retea = "Petrom")
        {
            var rezultate = await GetPreturiCarburantAsync(locatie, tip, nume_locatie, retea);
            return Ok(rezultate.Select(r => new { pret = r.Pret, adresa = r.Adresa }));
        }

        private async Task<List<(string Pret, string Adresa)>> GetPreturiCarburantAsync(string locatie, string tip, string nume_locatie, string retea)
        {
            var url = "https://www.peco-online.ro/index.php";
            using var httpClient = new HttpClient();

            // Adăugăm headere pentru a simula un browser real
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ro-RO,ro;q=0.9,en;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            // Construim lista de rețele - folosim toate rețelele principale dacă nu e specificată una anume
            var retele = new List<string>();
            if (!string.IsNullOrEmpty(retea))
            {
                retele.Add(retea);
            }
            else
            {
                // Rețelele principale cu prețuri
                retele.AddRange(new[] { "Gazprom", "Lukoil", "Mol", "OMV", "Petrom", "Rompetrol", "Socar" });
            }

            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("carburant", tip),
                new KeyValuePair<string, string>("locatie", locatie),
                new KeyValuePair<string, string>("nume_locatie", nume_locatie)
            };

            // Adăugăm toate rețelele
            foreach (var ret in retele)
            {
                formData.Add(new KeyValuePair<string, string>("retele[]", ret));
            }

            try
            {
                var content = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Request failed with status {response.StatusCode}");
                    return new List<(string, string)>();
                }

                var html = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Received HTML response of length: {html.Length}");
                
                return ParseNewFormatResponse(html);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching prices for {nume_locatie}");
                return new List<(string, string)>();
            }
        }

        private List<(string Pret, string Adresa)> ParseNewFormatResponse(string html)
        {
            var results = new List<(string Pret, string Adresa)>();

            try
            {
                // Căutăm rezultatele în format JSON din JavaScript
                var jsonPattern = @"var rezultate = JSON\.parse\('(.+?)'\);";
                var jsonMatch = Regex.Match(html, jsonPattern, RegexOptions.Singleline);

                if (jsonMatch.Success && jsonMatch.Groups[1].Value != "null")
                {
                    var jsonString = jsonMatch.Groups[1].Value;
                    // Decodificăm escape-urile din JSON
                    jsonString = jsonString.Replace("\\/", "/").Replace("\\\"", "\"");
                    
                    _logger.LogInformation($"Found JSON data: {jsonString}");
                    
                    var jsonDocument = JsonDocument.Parse(jsonString);
                    
                    foreach (var item in jsonDocument.RootElement.EnumerateArray())
                    {
                        if (item.GetArrayLength() >= 6)
                        {
                            var retea = item[0].GetString(); // numele rețelei
                            var lat = item[1].GetDouble();   // latitudine
                            var lon = item[2].GetDouble();   // longitudine  
                            var adresa = item[3].GetString(); // adresa
                            var detalii = item[4].GetString(); // detalii suplimentare
                            var pret = item[5].GetString();  // prețul
                            
                            if (!string.IsNullOrEmpty(pret) && IsValidPrice(pret))
                            {
                                results.Add((pret, $"{retea} - {adresa}"));
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No JSON rezultate found, trying to parse HTML table as fallback");
                    // Fallback la metoda veche pentru cazul în care site-ul mai are și tabele
                    return ParseLegacyTableFormat(html);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JSON response, trying HTML fallback");
                return ParseLegacyTableFormat(html);
            }

            _logger.LogInformation($"Parsed {results.Count} results from JSON");
            return results;
        }

        private List<(string Pret, string Adresa)> ParseLegacyTableFormat(string html)
        {
            var results = new List<(string Pret, string Adresa)>();
            
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//table//tr");
                if (rows != null)
                {
                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes("td");
                        if (cells != null && cells.Count >= 2)
                        {
                            var pret = cells[0].InnerText.Trim();
                            var adresa = cells[1].InnerText.Trim();
                            
                            if (IsValidPrice(pret))
                            {
                                results.Add((pret, adresa));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing legacy table format");
            }

            return results;
        }

        private bool IsValidPrice(string pret)
        {
            if (string.IsNullOrEmpty(pret) || pret == "N/A") return false;
            
            var parsedPrice = ParsePrice(pret);
            return parsedPrice > 3 && parsedPrice < 20; // Preț rezonabil pentru combustibil
        }

        private decimal ParsePrice(string pret)
        {
            if (string.IsNullOrEmpty(pret)) return 0;
            
            // Înlocuim virgula cu punct pentru parsing
            var cleanPrice = pret.Replace(",", ".").Trim();
            
            if (decimal.TryParse(cleanPrice, out var price))
            {
                return price;
            }
            
            return 0;
        }
    }
} 