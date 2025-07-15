using System.ComponentModel.DataAnnotations;

namespace TravelThingBackend.Models
{
    public class FuelPrice
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string City { get; set; }
        
        [Required]
        public string FuelType { get; set; }
        
        [Required]
        public decimal Price { get; set; }
        
        [Required]
        public DateTime LastUpdated { get; set; }
    }

    public class City
    {
        public const string Cluj = "Cluj";
        public const string Gorj = "Gorj";
        public const string Prahova = "Prahova";
        public const string Constanta = "Constanta";
        public const string Ilfov = "Ilfov";
        public const string Arad = "Arad";
        public const string Timisoara = "Timisoara";
        public const string Suceava = "Suceava";

        public const string Brasov = "Brasov";
        public const string Sibiu = "Sibiu";
        public const string Oradea = "Oradea";
        public const string Iasi = "Iasi";
        public const string Bacau = "Bacau";
        public const string Botosani = "Botosani";
        public const string SatuMare = "Satu Mare";
        public const string TarguMures = "Targu Mures";

        public static readonly string[] AllCities = new[]
        {
            Cluj,
            Gorj,
            Prahova,
            Constanta,
            Ilfov,
            Arad,
            Timisoara,
            Suceava,
            Brasov,
            Sibiu,
            Oradea,
            Iasi,
            Bacau,
            Botosani,
            SatuMare,
            TarguMures
        };
    }

    public class FuelType
    {
        public const string MotorinaPremium = "Motorina Premium";
        public const string MotorinaStandard = "Motorina Standard";
        public const string BenzinaStandard = "Benzina Standard";
        public const string BenzinaSuperioara = "Benzina Superioara";
        public const string GPL = "GPL";
        public const string Electric = "Electric";

        public static readonly string[] AllTypes = new[]
        {
            MotorinaPremium,
            MotorinaStandard,
            BenzinaStandard,
            BenzinaSuperioara,
            GPL,
            Electric
        };
    }
} 