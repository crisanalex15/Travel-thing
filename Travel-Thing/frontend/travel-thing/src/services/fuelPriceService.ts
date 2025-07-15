import axios from "axios";

const API_URL = "http://localhost:5283/api";

export const FUEL_TYPES = {
  BENZINA_STANDARD: "Benzina Standard",
  MOTORINA_STANDARD: "Motorina Standard",
  BENZINA_SUPERIOARA: "Benzina Superioara",
  MOTORINA_PREMIUM: "Motorina Premium",
  GPL: "GPL",
} as const;

export interface FuelPrice {
  id: number;
  city: string;
  fuelType: string;
  price: number;
  lastUpdated: string;
}

export type AveragePrices = Record<string, number>;

// Interfețe pentru noul endpoint inteligent
export interface RouteWithFuelRequest {
  startLocation?: string;
  endLocation?: string;
  startCoordinates?: number[]; // [longitude, latitude]
  endCoordinates?: number[]; // [longitude, latitude]
  preference?: string;
  fuelType?: string;
  fuelConsumption?: number;
}

export interface RouteWithFuelResponse {
  route: {
    distance: {
      meters: number;
      kilometers: number;
    };
    duration: {
      seconds: number;
      minutes: number;
      hours: number;
    };
    geometry: string;
  };
  fuelCalculation: {
    distance_km: number;
    fuel_type: string;
    consumption_per_100km: number;
    fuel_needed_liters: number;
    fuel_price_per_liter: number;
    total_fuel_cost: number;
    price_source: string;
    smart_routing: string;
  };
  recommendations: {
    save_money_tip: string;
    fuel_stations: string;
  };
}

export const fuelPriceService = {
  async getAllPrices(): Promise<FuelPrice[]> {
    try {
      const response = await axios.get(`${API_URL}/FuelPrice`);
      return response.data;
    } catch (error) {
      console.error("Eroare la obținerea prețurilor:", error);
      throw error;
    }
  },

  async getPricesByCity(city: string): Promise<FuelPrice[]> {
    try {
      const response = await axios.get(`${API_URL}/FuelPrice/city/${city}`);
      return response.data;
    } catch (error) {
      console.error(`Eroare la obținerea prețurilor pentru ${city}:`, error);
      throw error;
    }
  },

  async getAveragePrices(): Promise<AveragePrices> {
    try {
      console.log("Se face request către:", `${API_URL}/FuelPrice/average`);
      const response = await axios.get(`${API_URL}/FuelPrice/average`);
      console.log("Răspuns primit:", response.data);
      return response.data;
    } catch (error) {
      console.error("Eroare la obținerea prețurilor medii:", error);
      throw error;
    }
  },

  async getAveragePriceForType(fuelType: string): Promise<number> {
    try {
      const prices = await this.getAveragePrices();
      return prices[fuelType] || 0;
    } catch (error) {
      console.error(
        `Eroare la obținerea prețului mediu pentru ${fuelType}:`,
        error
      );
      throw error;
    }
  },

  async getLastUpdate(): Promise<Date> {
    try {
      const response = await axios.get(`${API_URL}/FuelPrice/last-update`);
      return new Date(response.data);
    } catch (error) {
      console.error("Eroare la obținerea datei ultimei actualizări:", error);
      throw error;
    }
  },

  // 🚀 NOUA FUNCȚIE INTELIGENTĂ pentru calcul rutei cu combustibil
  async calculateRouteWithFuel(
    request: RouteWithFuelRequest
  ): Promise<RouteWithFuelResponse> {
    try {
      console.log("🚗 Se calculează ruta cu endpoint-ul inteligent:", request);

      // Mapăm tipurile de combustibil din frontend la backend
      const fuelTypeMapping: Record<string, string> = {
        "Motorina Premium": "Motorina_Premium",
        "Motorina Standard": "Motorina_Regular",
        "Benzina Standard": "Benzina_Regular",
        "Benzina Superioara": "Benzina_Premium",
        GPL: "GPL",
      };

      const requestData = {
        ...request,
        fuelType: request.fuelType
          ? fuelTypeMapping[request.fuelType] || "Benzina_Regular"
          : "Benzina_Regular",
      };

      const response = await axios.post(
        `${API_URL}/RouteCalculator/calculate-with-fuel`,
        requestData
      );

      console.log("✅ Răspuns de la endpoint-ul inteligent:", response.data);
      return response.data;
    } catch (error) {
      console.error("❌ Eroare la calculul rutei cu combustibil:", error);
      throw error;
    }
  },

  // Funcție de fallback pentru calculul tradițional
  async calculateBasicRoute(
    request: Omit<RouteWithFuelRequest, "fuelType" | "fuelConsumption">
  ) {
    try {
      console.log("📍 Se calculează ruta basic:", request);
      const response = await axios.post(
        `${API_URL}/RouteCalculator/calculate`,
        request
      );
      return response.data;
    } catch (error) {
      console.error("Eroare la calculul rutei basic:", error);
      throw error;
    }
  },
};
