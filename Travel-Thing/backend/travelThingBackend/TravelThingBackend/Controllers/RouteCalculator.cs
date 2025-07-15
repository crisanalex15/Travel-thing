using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TravelThingBackend.Data;
using TravelThingBackend.Models;

namespace TravelThingBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RouteCalculatorController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RouteCalculatorController> _logger;
        private readonly ApplicationDbContext _context;
        private const string API_KEY = "5b3ce3597851110001cf6248f0889efb97c846d1869301f22972db7e";

        public RouteCalculatorController(HttpClient httpClient, ILogger<RouteCalculatorController> logger, ApplicationDbContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;
        }

        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateRoute([FromBody] RouteRequest request)
        {
            try
            {
                // Verificăm dacă avem locații sau coordonate
                bool hasStartLocation = !string.IsNullOrEmpty(request?.StartLocation);
                bool hasEndLocation = !string.IsNullOrEmpty(request?.EndLocation);
                bool hasStartCoords = request?.StartCoordinates != null && request.StartCoordinates.Length == 2;
                bool hasEndCoords = request?.EndCoordinates != null && request.EndCoordinates.Length == 2;

                if (!hasStartLocation && !hasStartCoords)
                {
                    return BadRequest(new { error = "Locația sau coordonatele de start sunt obligatorii" });
                }

                if (!hasEndLocation && !hasEndCoords)
                {
                    return BadRequest(new { error = "Locația sau coordonatele de sfârșit sunt obligatorii" });
                }

                _logger.LogInformation($"Calculare rută: Start - {(hasStartLocation ? request.StartLocation : $"[{request.StartCoordinates[0]}, {request.StartCoordinates[1]}]")}, End - {(hasEndLocation ? request.EndLocation : $"[{request.EndCoordinates[0]}, {request.EndCoordinates[1]}]")}");

                double[] startCoords;
                double[] endCoords;

                // Obținem coordonatele pentru punctul de start
                if (hasStartCoords)
                {
                    startCoords = request.StartCoordinates;
                    _logger.LogInformation($"Folosesc coordonate directe pentru start: [{startCoords[0]}, {startCoords[1]}]");
                }
                else
                {
                    startCoords = await GetCoordinatesFromLocation(request.StartLocation);
                }

                // Obținem coordonatele pentru punctul de sfârșit
                if (hasEndCoords)
                {
                    endCoords = request.EndCoordinates;
                    _logger.LogInformation($"Folosesc coordonate directe pentru sfârșit: [{endCoords[0]}, {endCoords[1]}]");
                }
                else
                {
                    endCoords = await GetCoordinatesFromLocation(request.EndLocation);
                }

                _logger.LogInformation($"Coordonate finale - Start: [{startCoords[0]}, {startCoords[1]}], End: [{endCoords[0]}, {endCoords[1]}]");

                var url = "https://api.openrouteservice.org/v2/directions/driving-car";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY);

                var preference = request.Preference?.ToLower() switch
                {
                    "fastest" => "fastest",
                    "shortest" => "shortest",
                    _ => "recommended"
                };

                var requestData = new
                {
                    coordinates = new[] { startCoords, endCoords },
                    preference = preference,
                    instructions = true,
                    format = "json"
                };

                _logger.LogInformation($"Request către OpenRouteService: {JsonConvert.SerializeObject(requestData)}");

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Eroare OpenRouteService - Status: {response.StatusCode}, Content: {errorContent}");
                    return BadRequest(new { error = $"Eroare la calcularea rutei: {response.StatusCode}. Verificați coordonatele sau locațiile introduse." });
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Răspuns primit de la OpenRouteService: {jsonResponse}");

                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                if (result?.routes == null || result.routes.Count == 0)
                {
                    return BadRequest(new { error = "Nu s-a putut calcula ruta" });
                }

                var route = result.routes[0];
                if (route?.summary == null)
                {
                    return BadRequest(new { error = "Nu s-au găsit detalii despre rută" });
                }

                var distance = route.summary.distance;
                var duration = route.summary.duration;
                var geometry = route.geometry?.ToString();

                if (distance == null || duration == null || string.IsNullOrEmpty(geometry))
                {
                    return BadRequest(new { error = "Nu s-au putut obține detaliile rutei" });
                }

                var responseData = new
                {
                    distance = new
                    {
                        meters = Math.Round((double)distance),
                        kilometers = Math.Round((double)distance / 1000, 1)
                    },
                    duration = new
                    {
                        seconds = Math.Round((double)duration),
                        minutes = Math.Round((double)duration / 60),
                        hours = Math.Round((double)duration / 3600, 1)
                    },
                    geometry = geometry
                };

                return Ok(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la calcularea rutei");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("calculate-with-fuel")]
        public async Task<IActionResult> CalculateRouteWithFuel([FromBody] RouteWithFuelRequest request)
        {
            try
            {
                // Mai întâi calculăm ruta
                var routeRequest = new RouteRequest
                {
                    StartLocation = request.StartLocation,
                    EndLocation = request.EndLocation,
                    StartCoordinates = request.StartCoordinates,
                    EndCoordinates = request.EndCoordinates,
                    Preference = request.Preference
                };

                var routeResult = await CalculateRouteInternal(routeRequest);
                
                if (routeResult == null)
                {
                    return BadRequest(new { error = "Nu s-a putut calcula ruta" });
                }

                var distanceKm = (double)routeResult.distance.kilometers;
                var fuelType = request.FuelType ?? "Benzina_Regular";
                var consumption = request.FuelConsumption ?? 7.0; // L/100km default
                
                _logger.LogInformation("🚗 Distanță calculată: {Distance} km, Tip combustibil: {FuelType}", distanceKm, fuelType);

                decimal fuelPrice = 0;
                string priceSource = "";
                
                // LOGICA INTELIGENTĂ: Sub 200km = API nou, peste 200km = metoda tradițională
                if (distanceKm < 200)
                {
                    _logger.LogInformation("📍 Distanță < 200km - folosesc API-ul îmbunătățit din baza de date");
                    
                    // Găsim orașul cel mai apropiat pentru destinație
                    var nearestCity = await FindNearestCityFromCoordinates(request.EndCoordinates);
                    fuelPrice = await GetFuelPriceFromDatabase(nearestCity, fuelType);
                    priceSource = $"DB-{nearestCity}";
                    
                    if (fuelPrice == 0)
                    {
                        // Fallback la prețul mediu din baza de date
                        fuelPrice = await GetAverageFuelPriceFromDatabase(fuelType);
                        priceSource = "DB-Average";
                    }
                }
                else
                {
                    _logger.LogInformation("🛣️ Distanță >= 200km - folosesc metoda tradițională de scraping");
                    
                    // Pentru distanțe mari, folosim scraping-ul direct
                    var nearestCity = await FindNearestCityFromCoordinates(request.EndCoordinates);
                    fuelPrice = await GetFuelPriceFromScraping(nearestCity, fuelType);
                    priceSource = $"Scraping-{nearestCity}";
                    
                    if (fuelPrice == 0)
                    {
                        // Fallback la baza de date
                        fuelPrice = await GetAverageFuelPriceFromDatabase(fuelType);
                        priceSource = "DB-Fallback";
                    }
                }
                
                // Calculăm costul combustibilului
                var fuelNeeded = (distanceKm / 100) * consumption;
                var fuelCost = fuelNeeded * (double)fuelPrice;
                
                var response = new
                {
                    route = routeResult,
                    fuelCalculation = new
                    {
                        distance_km = Math.Round(distanceKm, 1),
                        fuel_type = MapFuelTypeForDisplay(fuelType),
                        consumption_per_100km = consumption,
                        fuel_needed_liters = Math.Round(fuelNeeded, 2),
                        fuel_price_per_liter = Math.Round((double)fuelPrice, 3),
                        total_fuel_cost = Math.Round(fuelCost, 2),
                        price_source = priceSource,
                        smart_routing = distanceKm < 200 ? "Database API" : "Live Scraping"
                    },
                    recommendations = new
                    {
                        save_money_tip = fuelPrice > 0 ? $"Prețul combustibilului este {fuelPrice:F3} RON/litru" : "Nu s-au găsit prețuri actuale",
                        fuel_stations = "Căutați benzinării Petrom, OMV sau Rompetrol pentru prețuri competitive"
                    }
                };

                _logger.LogInformation("✅ Calcul completat: {Distance}km, {FuelCost} RON combustibil (sursa: {Source})", 
                    (object)distanceKm, (object)Math.Round(fuelCost, 2), (object)priceSource);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la calcularea rutei cu combustibil");
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task<dynamic> CalculateRouteInternal(RouteRequest request)
        {
            // Verificăm dacă avem locații sau coordonate
            bool hasStartLocation = !string.IsNullOrEmpty(request?.StartLocation);
            bool hasEndLocation = !string.IsNullOrEmpty(request?.EndLocation);
            bool hasStartCoords = request?.StartCoordinates != null && request.StartCoordinates.Length == 2;
            bool hasEndCoords = request?.EndCoordinates != null && request.EndCoordinates.Length == 2;

            if (!hasStartLocation && !hasStartCoords)
            {
                return null;
            }

            if (!hasEndLocation && !hasEndCoords)
            {
                return null;
            }

            double[] startCoords;
            double[] endCoords;

            // Obținem coordonatele pentru punctul de start
            if (hasStartCoords)
            {
                startCoords = request.StartCoordinates;
            }
            else
            {
                startCoords = await GetCoordinatesFromLocation(request.StartLocation);
            }

            // Obținem coordonatele pentru punctul de sfârșit
            if (hasEndCoords)
            {
                endCoords = request.EndCoordinates;
            }
            else
            {
                endCoords = await GetCoordinatesFromLocation(request.EndLocation);
            }

            var url = "https://api.openrouteservice.org/v2/directions/driving-car";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY);

            var preference = request.Preference?.ToLower() switch
            {
                "fastest" => "fastest",
                "shortest" => "shortest",
                _ => "recommended"
            };

            var requestData = new
            {
                coordinates = new[] { startCoords, endCoords },
                preference = preference,
                instructions = true,
                format = "json"
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            if (result?.routes == null || result.routes.Count == 0)
            {
                return null;
            }

            var route = result.routes[0];
            if (route?.summary == null)
            {
                return null;
            }

            var distance = route.summary.distance;
            var duration = route.summary.duration;
            var geometry = route.geometry?.ToString();

            if (distance == null || duration == null || string.IsNullOrEmpty(geometry))
            {
                return null;
            }

            return new
            {
                distance = new
                {
                    meters = Math.Round((double)distance),
                    kilometers = Math.Round((double)distance / 1000, 1)
                },
                duration = new
                {
                    seconds = Math.Round((double)duration),
                    minutes = Math.Round((double)duration / 60),
                    hours = Math.Round((double)duration / 3600, 1)
                },
                geometry = geometry
            };
        }

        private async Task<double[]> GetCoordinatesFromLocation(string location)
        {
            try
            {
                string url = $"https://api.openrouteservice.org/geocode/search?api_key={API_KEY}&text={Uri.EscapeDataString(location)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Răspuns geocoding pentru {location}: {json}");

                dynamic result = JsonConvert.DeserializeObject(json);

                if (result?.features == null || result.features.Count == 0)
                {
                    throw new Exception($"Nu s-a găsit locația: {location}");
                }

                var coordinates = result.features[0].geometry.coordinates;
                if (coordinates == null || coordinates.Count < 2)
                {
                    throw new Exception($"Coordonate invalide pentru locația: {location}");
                }

                return new double[] { (double)coordinates[0], (double)coordinates[1] }; // [lon, lat]
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Eroare la geocoding pentru {location}");
                throw;
            }
        }

        private async Task<string> FindNearestCityFromCoordinates(double[] coordinates)
        {
            if (coordinates == null || coordinates.Length < 2)
                return "Cluj"; // Default fallback
            
            var cities = City.AllCities;
            var cityCoordinates = new Dictionary<string, (double lat, double lon)>
            {
                {"Cluj", (46.7712, 23.6236)},
                {"Gorj", (44.9147, 23.2719)},
                {"Prahova", (45.1, 26.0)},
                {"Constanta", (44.1598, 28.6348)},
                {"Ilfov", (44.5, 26.1)},
                {"Arad", (46.1866, 21.3123)},
                {"Timisoara", (45.7489, 21.2087)},
                {"Suceava", (47.6635, 26.2535)}
            };

            var targetLat = coordinates[1]; // latitude
            var targetLon = coordinates[0]; // longitude
            
            var nearestCity = "Cluj";
            var minDistance = double.MaxValue;
            
            foreach (var city in cityCoordinates)
            {
                var distance = CalculateDistance(targetLat, targetLon, city.Value.lat, city.Value.lon);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCity = city.Key;
                }
            }
            
            _logger.LogInformation("🎯 Cel mai apropiat oraș: {City} (distanță: {Distance:F1} km)", nearestCity, minDistance);
            return nearestCity;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private async Task<decimal> GetFuelPriceFromDatabase(string city, string fuelType)
        {
            var mappedFuelType = MapFuelTypeFromRequest(fuelType);
            
            var price = await _context.FuelPrices
                .Where(p => p.City == city && p.FuelType == mappedFuelType)
                .OrderByDescending(p => p.LastUpdated)
                .Select(p => p.Price)
                .FirstOrDefaultAsync();
                
            _logger.LogInformation("💾 Preț din baza de date pentru {City}-{FuelType}: {Price}", city, mappedFuelType, price);
            return price;
        }

        private async Task<decimal> GetAverageFuelPriceFromDatabase(string fuelType)
        {
            var mappedFuelType = MapFuelTypeFromRequest(fuelType);
            
            var avgPrice = await _context.FuelPrices
                .Where(p => p.FuelType == mappedFuelType)
                .AverageAsync(p => p.Price);
                
            _logger.LogInformation("📊 Preț mediu din baza de date pentru {FuelType}: {Price}", mappedFuelType, avgPrice);
            return avgPrice;
        }

        private async Task<decimal> GetFuelPriceFromScraping(string city, string fuelType)
        {
            try
            {
                // Folosim CarburantController logic direct aici
                var url = "https://www.peco-online.ro/index.php";
                using var httpClient = new HttpClient();

                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("carburant", fuelType),
                    new KeyValuePair<string, string>("locatie", "Judet"),
                    new KeyValuePair<string, string>("nume_locatie", city)
                };

                var networks = new[] { "Petrom", "OMV", "Rompetrol", "Lukoil", "Mol", "Socar", "Gazprom" };
                foreach (var network in networks)
                {
                    formData.Add(new KeyValuePair<string, string>("retele[]", network));
                }

                var content = new FormUrlEncodedContent(formData);
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    var price = ExtractPriceFromHtml(html);
                    _logger.LogInformation("🌐 Preț din scraping pentru {City}-{FuelType}: {Price}", city, fuelType, price);
                    return price;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la scraping pentru {City}-{FuelType}", city, fuelType);
            }
            
            return 0;
        }

        private decimal ExtractPriceFromHtml(string html)
        {
            // Simplified price extraction - you can use the complex logic from FuelPriceService
            try
            {
                if (html.Contains("var rezultate = JSON.parse('") && !html.Contains("var rezultate = JSON.parse('null')"))
                {
                    var start = html.IndexOf("var rezultate = JSON.parse('") + "var rezultate = JSON.parse('".Length;
                    var end = html.IndexOf("');", start);
                    if (end > start)
                    {
                        var jsonString = html.Substring(start, end - start);
                        // Basic JSON parsing for price extraction
                        if (jsonString.Contains("[") && jsonString.Contains("]"))
                        {
                            // Look for price patterns in JSON
                            var priceMatch = System.Text.RegularExpressions.Regex.Match(jsonString, @",(\d+\.?\d*)\]");
                            if (priceMatch.Success)
                            {
                                if (decimal.TryParse(priceMatch.Groups[1].Value, out var price))
                                {
                                    return price;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la extragerea prețului din HTML");
            }
            
            return 0;
        }

        private string MapFuelTypeFromRequest(string requestFuelType)
        {
            return requestFuelType switch
            {
                "Benzina_Regular" => "Benzina Standard",
                "Motorina_Regular" => "Motorina Standard",
                "GPL" => "GPL",
                "Benzina_Premium" => "Benzina Superioara",
                "Motorina_Premium" => "Motorina Premium",
                _ => "Benzina Standard"
            };
        }

        private string MapFuelTypeForDisplay(string internalFuelType)
        {
            return internalFuelType switch
            {
                "Benzina_Regular" => "Benzină Regulată",
                "Motorina_Regular" => "Motorină Standard",
                "GPL" => "GPL",
                "Benzina_Premium" => "Benzină Premium",
                "Motorina_Premium" => "Motorină Premium",
                _ => internalFuelType
            };
        }
    }

    public class RouteRequest
    {
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public double[]? StartCoordinates { get; set; } // [longitude, latitude]
        public double[]? EndCoordinates { get; set; } // [longitude, latitude]
        public string Preference { get; set; } = "recommended";
    }

    public class RouteWithFuelRequest
    {
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public double[]? StartCoordinates { get; set; } // [longitude, latitude]
        public double[]? EndCoordinates { get; set; } // [longitude, latitude]
        public string Preference { get; set; } = "recommended";
        public string? FuelType { get; set; }
        public double? FuelConsumption { get; set; }
    }
}