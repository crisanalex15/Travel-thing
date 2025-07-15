using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelThingBackend.Data;
using TravelThingBackend.Models;
using TravelThingBackend.Services;

namespace TravelThingBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FuelPriceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly FuelPriceService _fuelPriceService;
        private readonly ILogger<FuelPriceController> _logger;

        public FuelPriceController(
            ApplicationDbContext context,
            FuelPriceService fuelPriceService,
            ILogger<FuelPriceController> logger)
        {
            _context = context;
            _fuelPriceService = fuelPriceService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FuelPrice>>> GetAllPrices()
        {
            return await _context.FuelPrices
                .OrderBy(p => p.City)
                .ThenBy(p => p.FuelType)
                .ToListAsync();
        }

        [HttpGet("city/{city}")]
        public async Task<ActionResult<IEnumerable<FuelPrice>>> GetPricesByCity(string city)
        {
            var prices = await _context.FuelPrices
                .Where(p => p.City == city)
                .OrderBy(p => p.FuelType)
                .ToListAsync();

            if (!prices.Any())
            {
                return NotFound($"Nu s-au găsit prețuri pentru orașul {city}");
            }

            return prices;
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdatePrices()
        {
            try
            {
                await _fuelPriceService.UpdateFuelPricesAsync();
                return Ok("Prețurile au fost actualizate cu succes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eroare la actualizarea prețurilor");
                return StatusCode(500, "A apărut o eroare la actualizarea prețurilor");
            }
        }

        [HttpPost("test-update")]
        public async Task<IActionResult> TestUpdate([FromQuery] string city = "Cluj")
        {
            try
            {
                _logger.LogInformation("Testing price update for city: {City}", city);
                
                // Testăm doar pentru un oraș și un tip de combustibil
                var testResult = await _fuelPriceService.TestSingleCityUpdate(city, "Benzina_Regular");
                
                return Ok(new 
                { 
                    success = testResult.Success,
                    city = city,
                    fuel_type = "Benzina_Regular",
                    price = testResult.Price,
                    message = testResult.Message,
                    debug_info = testResult.DebugInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test update failed");
                return Ok(new 
                { 
                    success = false,
                    error = ex.Message,
                    message = "Test update failed"
                });
            }
        }

        [HttpGet("average")]
        public async Task<ActionResult<Dictionary<string, decimal>>> GetAveragePrices()
        {
            _logger.LogInformation("Se solicită prețurile medii");
            
            var prices = await _context.FuelPrices
                .GroupBy(p => p.FuelType)
                .Select(g => new
                {
                    FuelType = g.Key,
                    AveragePrice = Math.Round(g.Average(p => p.Price), 2),
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.FuelType, x => x.AveragePrice);

            _logger.LogInformation("Prețuri medii găsite: {Prices}", 
                string.Join(", ", prices.Select(p => $"{p.Key}: {p.Value}")));

            return prices;
        }

        [HttpGet("last-update")]
        public async Task<ActionResult<DateTime>> GetLastUpdate()
        {
            var lastUpdate = await _context.FuelPrices
                .OrderByDescending(p => p.LastUpdated)
                .Select(p => p.LastUpdated)
                .FirstOrDefaultAsync();

            if (lastUpdate == null)
            {
                return NotFound("Nu există prețuri în baza de date");
            }

            return lastUpdate;
        }
    }
} 