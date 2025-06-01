/*5ae2e3f221c38a28845f05b669b15222856ba95debb9052a3c22159a*/
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class TouristAttractionsController : ControllerBase
{
    private readonly ILogger<TouristAttractionsController> _logger;
    private readonly HttpClient _httpClient;
    private const string ApiKey = "5ae2e3f221c38a28845f05b669b15222856ba95debb9052a3c22159a"; // înlocuiește cu cheia ta
    private const string BaseUrl = "https://api.opentripmap.com/0.1/en/places";
    private const string GoogleApiKey = "YOUR_GOOGLE_API_KEY"; // Înlocuiește cu cheia ta

    public TouristAttractionsController(HttpClient httpClient, ILogger<TouristAttractionsController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Raza Pământului în kilometri
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

    private async Task<string> GetGooglePlaceImage(string placeName, double lat, double lon)
    {
        try
        {
            // Căutăm locația în Google Places
            var searchUrl = $"https://maps.googleapis.com/maps/api/place/textsearch/json?query={Uri.EscapeDataString(placeName)}&location={lat},{lon}&radius=1000&key={GoogleApiKey}";
            var searchResponse = await _httpClient.GetAsync(searchUrl);
            
            if (!searchResponse.IsSuccessStatusCode)
                return "https://via.placeholder.com/300x200?text=No+Image+Available";

            var searchJson = await searchResponse.Content.ReadAsStringAsync();
            var searchData = JsonDocument.Parse(searchJson).RootElement;

            if (searchData.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var photo in photos.EnumerateArray())
                        {
                            if (photo.TryGetProperty("photo_reference", out var photoRef))
                            {
                                var photoReference = photoRef.GetString();
                                return $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=400&photoreference={photoReference}&key={GoogleApiKey}";
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // În caz de eroare, returnăm imaginea placeholder
        }

        return "https://via.placeholder.com/300x200?text=No+Image+Available";
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearbyAttractions(
        double lat,
        double lon,
        int radius = 10000,
        string kinds = "interesting_places")
    {
        var url = $"{BaseUrl}/radius?radius={radius}&lon={lon}&lat={lat}&kinds={kinds}&format=json&apikey={ApiKey}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Eroare la apelarea OpenTripMap");

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(json).RootElement;
        
        var results = new List<object>();

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var feature in data.EnumerateArray())
            {
                try
                {
                    var xid = feature.GetProperty("xid").GetString();
                    var detailsUrl = $"{BaseUrl}/xid/{xid}?apikey={ApiKey}";
                    var detailsResponse = await _httpClient.GetAsync(detailsUrl);

                    if (!detailsResponse.IsSuccessStatusCode)
                        continue;

                    var detailJson = await detailsResponse.Content.ReadAsStringAsync();
                    var detail = JsonDocument.Parse(detailJson).RootElement;

                    var name = detail.GetProperty("name").GetString();
                    var kindsList = detail.GetProperty("kinds").GetString();
                    var point = detail.GetProperty("point");
                    var latDetail = point.GetProperty("lat").GetDouble();
                    var lonDetail = point.GetProperty("lon").GetDouble();

                    // Calculăm distanța
                    var distance = CalculateDistance(lat, lon, latDetail, lonDetail);

                    // Generăm link-ul către Google Maps
                    var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={latDetail},{lonDetail}";

                    // Extragem descrierea din mai multe surse posibile
                    string description = "Nu există descriere disponibilă";
                    if (detail.TryGetProperty("wikipedia_extracts", out var wiki) && wiki.ValueKind == JsonValueKind.Object)
                    {
                        description = wiki.GetProperty("text").GetString() ?? description;
                    }
                    else if (detail.TryGetProperty("descr", out var descr) && descr.ValueKind == JsonValueKind.String)
                    {
                        description = descr.GetString() ?? description;
                    }
                    else if (detail.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
                    {
                        description = info.GetProperty("descr").GetString() ?? description;
                    }
                    else if (detail.TryGetProperty("wikipedia", out var wikiText) && wikiText.ValueKind == JsonValueKind.String)
                    {
                        description = wikiText.GetString() ?? description;
                    }

                    // Încercăm să obținem imaginea din Google Places
                    string image = await GetGooglePlaceImage(name, latDetail, lonDetail);

                    results.Add(new
                    {
                        Name = name,
                        Description = description,
                        Image = image,
                        Lat = latDetail,
                        Lon = lonDetail,
                        Kinds = kindsList,
                        Distance = Math.Round(distance, 2), // Distanța în kilometri, rotunjită la 2 zecimale
                        MapsUrl = mapsUrl
                    });
                }
                catch (Exception ex)
                {
                    // Continuăm cu următoarea atracție dacă avem o eroare
                    continue;
                }
            }
        }

        return Ok(results);
    }

    [HttpGet("nearbyZone")]
    public async Task<IActionResult> GetNearbyAttractions(
        string location = null,
        double? lat = null,
        double? lon = null,
        int radius = 10000,
        string kinds = "interesting_places")
    {
        try
        {
            double latitude;
            double longitude;

            // Verificăm dacă avem coordonate sau locație
            if (lat.HasValue && lon.HasValue)
            {
                latitude = lat.Value;
                longitude = lon.Value;
                _logger.LogInformation($"Folosesc coordonate directe: [{longitude}, {latitude}]");
            }
            else if (!string.IsNullOrEmpty(location))
            {
                var coordinates = await GetCoordinatesFromLocation(location);
                longitude = coordinates[0];
                latitude = coordinates[1];
                _logger.LogInformation($"Coordonate obținute pentru {location}: [{longitude}, {latitude}]");
            }
            else
            {
                return BadRequest(new { error = "Trebuie să specificați fie o locație, fie coordonate (lat/lon)" });
            }

            // Împărțim kinds în array și eliminăm spațiile
            var kindsArray = kinds.Split(',').Select(k => k.Trim()).ToArray();
            var results = new List<object>();

            foreach (var kind in kindsArray)
            {
                try
                {
                    var url = $"{BaseUrl}/radius?radius={radius}&lon={longitude}&lat={latitude}&kinds={kind}&format=json&apikey={ApiKey}";
                    _logger.LogInformation($"Căutare pentru kind: {kind}, URL: {url}");

                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Eroare la căutarea pentru kind {kind}: {response.StatusCode}");
                        continue;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonDocument.Parse(json).RootElement;

                    if (data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var feature in data.EnumerateArray())
                        {
                            try
                            {
                                var xid = feature.GetProperty("xid").GetString();
                                var detailsUrl = $"{BaseUrl}/xid/{xid}?apikey={ApiKey}";
                                var detailsResponse = await _httpClient.GetAsync(detailsUrl);

                                if (!detailsResponse.IsSuccessStatusCode)
                                    continue;

                                var detailJson = await detailsResponse.Content.ReadAsStringAsync();
                                var detail = JsonDocument.Parse(detailJson).RootElement;

                                var name = detail.GetProperty("name").GetString();
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                var kindsList = detail.GetProperty("kinds").GetString();
                                var point = detail.GetProperty("point");
                                var latDetail = point.GetProperty("lat").GetDouble();
                                var lonDetail = point.GetProperty("lon").GetDouble();

                                // Calculăm distanța
                                var distance = CalculateDistance(latitude, longitude, latDetail, lonDetail);

                                // Generăm link-ul către Google Maps
                                var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={latDetail},{lonDetail}";

                                // Extragem descrierea din mai multe surse posibile
                                string description = "Nu există descriere disponibilă";
                                if (detail.TryGetProperty("wikipedia_extracts", out var wiki) && wiki.ValueKind == JsonValueKind.Object)
                                {
                                    description = wiki.GetProperty("text").GetString() ?? description;
                                }
                                else if (detail.TryGetProperty("descr", out var descr) && descr.ValueKind == JsonValueKind.String)
                                {
                                    description = descr.GetString() ?? description;
                                }
                                else if (detail.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
                                {
                                    description = info.GetProperty("descr").GetString() ?? description;
                                }
                                else if (detail.TryGetProperty("wikipedia", out var wikiText) && wikiText.ValueKind == JsonValueKind.String)
                                {
                                    description = wikiText.GetString() ?? description;
                                }

                                // Verificăm dacă locația există deja în rezultate
                                var existingLocation = results.FirstOrDefault(r => 
                                    r.GetType().GetProperty("Name").GetValue(r).ToString() == name);
                                
                                if (existingLocation == null)
                                {
                                    // Adăugăm informații suplimentare pentru cazări
                                    string type = "Alte locații";
                                    if (kindsList.Contains("accomodations"))
                                    {
                                        if (kindsList.Contains("hotels")) type = "Hotel";
                                        else if (kindsList.Contains("hostels")) type = "Hostel";
                                        else if (kindsList.Contains("motels")) type = "Motel";
                                        else if (kindsList.Contains("camping")) type = "Camping";
                                        else if (kindsList.Contains("guest_houses")) type = "Pensiune";
                                        else if (kindsList.Contains("chalets")) type = "Cabană";
                                    }

                                    results.Add(new
                                    {
                                        Name = name,
                                        Description = description,
                                        Lat = latDetail,
                                        Lon = lonDetail,
                                        Kinds = kindsList,
                                        Distance = Math.Round(distance, 2),
                                        MapsUrl = mapsUrl,
                                        Type = type
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Eroare la procesarea detaliilor pentru locație");
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Eroare la procesarea kind-ului {kind}");
                    continue;
                }
            }

            // Sortăm rezultatele după distanță
            var sortedResults = results.OrderBy(r => 
                r.GetType().GetProperty("Distance").GetValue(r)).ToList();

            return Ok(sortedResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eroare la căutarea atracțiilor");
            return StatusCode(500, "Eroare la căutarea atracțiilor");
        }
    }

    private async Task<double[]> GetCoordinatesFromLocation(string location)
    {
        try
        {
            string url = $"https://api.openrouteservice.org/geocode/search?api_key={"5b3ce3597851110001cf6248f0889efb97c846d1869301f22972db7e"}&text={Uri.EscapeDataString(location)}";
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

    [HttpGet("attraction-types")]
    public IActionResult GetAttractionTypes()
    {
        var attractionTypes = new[]
        {
            // Cazări
            new { value = "accomodations.hotels", label = "Hoteluri" },
            new { value = "accomodations.hostels", label = "Hosteluri" },
            new { value = "accomodations.motels", label = "Moteluri" },
            new { value = "accomodations.camping", label = "Camping" },
            new { value = "accomodations.guest_houses", label = "Pensiuni" },
            new { value = "accomodations.chalets", label = "Cabane" },

            // Restaurante și cafenele
            new { value = "foods", label = "Restaurante" },
            new { value = "restaurants", label = "Restaurante" },
            new { value = "fast_food", label = "Fast Food" },
            new { value = "cuisine", label = "Restaurante" },
            new { value = "food", label = "Restaurante" },
            new { value = "cafes", label = "Cafenele" },
            new { value = "coffee", label = "Cafenele" },
            new { value = "tea", label = "Cafenele" },

            // Parcuri și natură
            new { value = "parks", label = "Parcuri" },
            new { value = "gardens", label = "Grădini" },
            new { value = "nature_reserves", label = "Rezervații naturale" },
            new { value = "natural_parks", label = "Parcuri naturale" },
            new { value = "national_parks", label = "Parcuri naționale" },

            // Cultură și artă
            new { value = "museums", label = "Muzee" },
            new { value = "art_galleries", label = "Galerii de artă" },
            new { value = "art", label = "Artă" },
            new { value = "history", label = "Istorie" },

            // Divertisment
            new { value = "theatres_and_entertainments", label = "Teatre și divertisment" },

            // Magazine și shopping
            new { value = "shops", label = "Magazine" },
            new { value = "shopping_malls", label = "Mall-uri" },
            new { value = "markets", label = "Piețe" },
            new { value = "supermarkets", label = "Supermarket-uri" },

            // Religie și arhitectură
            new { value = "churches", label = "Biserici" },
            new { value = "religion", label = "Locuri de cult" },
            new { value = "cathedrals", label = "Catedrale" },
            new { value = "eastern_orthodox_churches", label = "Biserici ortodoxe" },
            new { value = "other_churches", label = "Alte biserici" },

            // Monumente și arhitectură
            new { value = "monuments_and_memorials", label = "Monumente și memoriale" },
            new { value = "monuments", label = "Monumente" },
            new { value = "memorials", label = "Memoriale" },
            new { value = "architecture", label = "Arhitectură" },

            // Castele și fortificații
            new { value = "castles", label = "Castele" },
            new { value = "forts", label = "Fortificații" },
            new { value = "castles_and_forts", label = "Castele și fortificații" },

            // Alte atracții
            new { value = "interesting_places", label = "Locuri interesante" }
        };

        return Ok(attractionTypes);
    }
}

