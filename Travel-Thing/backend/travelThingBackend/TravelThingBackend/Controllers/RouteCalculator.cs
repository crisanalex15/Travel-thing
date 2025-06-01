using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace TravelThingBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RouteCalculatorController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RouteCalculatorController> _logger;
        private const string API_KEY = "5b3ce3597851110001cf6248f0889efb97c846d1869301f22972db7e";

        public RouteCalculatorController(HttpClient httpClient, ILogger<RouteCalculatorController> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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
    }

    public class RouteRequest
    {
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public double[]? StartCoordinates { get; set; } // [longitude, latitude]
        public double[]? EndCoordinates { get; set; } // [longitude, latitude]
        public string Preference { get; set; } = "recommended";
    }
}