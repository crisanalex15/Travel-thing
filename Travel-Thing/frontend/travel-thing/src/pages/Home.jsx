import React, { useState, useEffect } from "react";
import Button from "../components/Button";
import Input from "../components/Input";
import "./Home.css";
import { fuelPriceService } from "../services/fuelPriceService";
import AverageFuelPrices from "../components/AverageFuelPrices";
import TravelMap from "../components/map/map";
import Divider from "../components/Divider";

// Importăm imaginile
import cutleryIcon from "../components/Images/cutlery.png";
import cafeIcon from "../components/Images/latte-art.png";
import parkIcon from "../components/Images/park.png";
import museumIcon from "../components/Images/museum.png";
import theaterIcon from "../components/Images/theater.png";
import shopIcon from "../components/Images/store.png";
import churchIcon from "../components/Images/church.png";
import architectureIcon from "../components/Images/architecture.png";
import monumentIcon from "../components/Images/monument.png";
import castleIcon from "../components/Images/castle.png";
import lakeIcon from "../components/Images/bed.png";
import beachIcon from "../components/Images/question-mark.png";

/*const attractionTypeMap = {
  restaurants: "foods,restaurants,fast_food,cuisine,food",
  cafes: "cafes,coffee,tea",
  parks: "parks,gardens,nature_reserves,natural_parks,national_parks",
  museums: "museums,art_galleries,art,history",
  theaters: "theatres_and_entertainments",
  shops: "shops,shopping_malls,markets,supermarkets",
  churches:
    "churches,religion,cathedrals,eastern_orthodox_churches,other_churches",
  architecture: "monuments_and_memorials,monuments,memorials",
  monuments: "monuments_and_memorials,monuments,memorials",
  castles: "castles,forts,castles_and_forts",
  lakes: "hotels,hostels,motels,camping,guest_houses,chalets,accommodations",
  beaches: "interesting_places",
};
 */

const attractionTypeMap = {
  // Cazări
  lakes:
    "hotels,hostels,motels,camping,guest_houses,chalets,accommodations,apartments,bed_and_breakfasts,resorts,villas",

  // Restaurante și cafenele
  restaurants:
    "foods,restaurants,fast_food,cuisine,food,eating_and_drinking,restaurants,fast_food,cuisine,food,local_food,street_food",
  cafes: "cafes,coffee,tea,bars,pubs,nightclubs,drinking_water",

  // Parcuri și natură
  parks:
    "parks,gardens,nature_reserves,natural_parks,national_parks,beaches,forests,lakes,rivers,waterfalls,mountains,viewpoints,lookouts",

  // Cultură și artă
  museums:
    "museums,art_galleries,art,history,cultural_centers,exhibitions,historic_sites,archaeological_sites,ruins,monuments,memorials",
  theaters:
    "theatres_and_entertainments,cinemas,concert_halls,music_venues,stadiums,sports_venues",

  // Shopping și servicii
  shops:
    "shops,shopping_malls,markets,supermarkets,convenience_stores,department_stores,outlet_malls,local_shops,craft_shops,souvenir_shops",

  // Religie și arhitectură
  churches:
    "churches,religion,cathedrals,eastern_orthodox_churches,other_churches,temples,mosques,synagogues,monasteries,religious_sites",
  architecture:
    "monuments_and_memorials,monuments,memorials,architecture,historic_architecture,modern_architecture,landmarks",
  monuments:
    "monuments_and_memorials,monuments,memorials,historic_sites,archaeological_sites,ruins",

  // Castele și fortificații
  castles:
    "castles,forts,castles_and_forts,fortifications,defensive_walls,towers,watchtowers",

  // Transport și infrastructură
  beaches:
    "interesting_places,transportation,airports,railway_stations,bus_stations,ports,ferry_terminals,taxi_stands,car_rentals,bicycle_rentals,public_transport,subway_stations,tram_stops",

  // Divertisment și activități
  entertainment:
    "entertainment,amusement_parks,water_parks,zoos,aquariums,botanical_gardens,theme_parks,adventure_parks,playgrounds,recreation_areas",

  // Sănătate și wellness
  wellness:
    "health,pharmacies,hospitals,clinics,spas,wellness_centers,fitness_centers,yoga_centers",

  // Educație și știință
  education:
    "education,schools,universities,libraries,research_centers,science_centers,planetariums,observatories",

  // Servicii și utilități
  services:
    "services,banks,post_offices,police_stations,fire_stations,gas_stations,car_washes,repair_shops,laundry_services",
};

export default function Home() {
  const [startLocation, setStartLocation] = useState("");
  const [endLocation, setEndLocation] = useState("");
  const [startCoordinates, setStartCoordinates] = useState(null);
  const [endCoordinates, setEndCoordinates] = useState(null);
  const [isSelectingLocation, setIsSelectingLocation] = useState(false);
  const [selectionType, setSelectionType] = useState(null); // 'start' sau 'end'
  const [consumption, setConsumption] = useState("");
  const [fuelPrice, setFuelPrice] = useState("");
  const [manualPrice, setManualPrice] = useState(false);
  const [fuelType, setFuelType] = useState("Motorina Standard");
  const [routePreference, setRoutePreference] = useState("recommended");
  const [routeResult, setRouteResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [averagePrices, setAveragePrices] = useState({});
  const [fuelPrices, setFuelPrices] = useState({});
  const [isRoundTrip, setIsRoundTrip] = useState(false);
  const [selectedAttraction, setSelectedAttraction] = useState(null);
  const [radius, setRadius] = useState("");
  const [showAttractions, setShowAttractions] = useState(false);
  const [foundLocations, setFoundLocations] = useState([]);
  const [numberOfPeople, setNumberOfPeople] = useState(1);
  const [showSplitCost, setShowSplitCost] = useState(false);
  const [showExpandedAttractions, setShowExpandedAttractions] = useState(false);
  const [maxAttractions, setMaxAttractions] = useState(5);
  const [selectedAttractionCoords, setSelectedAttractionCoords] =
    useState(null);

  const fuelTypes = [
    "Motorina Premium",
    "Motorina Standard",
    "Benzina Standard",
    "Benzina Superioara",
    "GPL",
    "Electric",
  ];

  const routePreferences = [
    { value: "shortest", label: "Eco", icon: "🌱" },
    { value: "fastest", label: "Normal", icon: "🚗" },
    { value: "recommended", label: "Combinat", icon: "🔄" },
  ];

  useEffect(() => {
    const fetchPrices = async () => {
      try {
        console.log("Se încearcă obținerea prețurilor...");
        const prices = await fuelPriceService.getAveragePrices();
        console.log("Prețuri obținute:", prices);
        setFuelPrices(prices);
        setLoading(false);
      } catch (err) {
        console.error("Eroare detaliată:", err);
        setError(
          "Eroare la încărcarea prețurilor. Vă rugăm să încercați din nou."
        );
        setLoading(false);
      }
    };

    fetchPrices();
  }, []);

  const calculateFuelConsumption = () => {
    if (!routeResult || !consumption || parseFloat(consumption) <= 0) {
      return {
        fuelNeeded: 0,
        totalCost: 0,
        error: "Vă rugăm să introduceți un consum valid",
      };
    }

    const distanceInKm =
      routeResult.distance.kilometers * (isRoundTrip ? 2 : 1);
    const consumptionPerKm = parseFloat(consumption) / 100;
    const totalFuelNeeded = distanceInKm * consumptionPerKm;

    let costPerLiter;
    if (manualPrice) {
      if (!fuelPrice || parseFloat(fuelPrice) <= 0) {
        return {
          fuelNeeded: Math.round(totalFuelNeeded * 100) / 100,
          totalCost: 0,
          error: "Vă rugăm să introduceți un preț valid",
        };
      }
      costPerLiter = parseFloat(fuelPrice);
    } else {
      costPerLiter = fuelPrices[fuelType];
    }

    const totalCost = totalFuelNeeded * costPerLiter;

    return {
      fuelNeeded: Math.round(totalFuelNeeded * 100) / 100,
      totalCost: Math.round(totalCost * 100) / 100,
      error: null,
    };
  };

  const normalizeLocation = (location) => {
    if (!location) return "";
    return location
      .trim()
      .split(" ")
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
      .join(" ");
  };

  const handleRoutePreferenceChange = async (preference) => {
    setRoutePreference(preference);
    if (
      (startLocation || startCoordinates) &&
      (endLocation || endCoordinates)
    ) {
      setLoading(true);
      setError(null);
      try {
        const requestBody = {
          preference: preference,
        };

        // Adăugăm locația sau coordonatele de start
        if (startCoordinates) {
          requestBody.startCoordinates = [
            startCoordinates[1],
            startCoordinates[0],
          ]; // [lon, lat]
        } else {
          requestBody.startLocation = normalizeLocation(startLocation);
        }

        // Adăugăm locația sau coordonatele de destinație
        if (endCoordinates) {
          requestBody.endCoordinates = [endCoordinates[1], endCoordinates[0]]; // [lon, lat]
        } else {
          requestBody.endLocation = normalizeLocation(endLocation);
        }

        const response = await fetch(
          "http://localhost:5283/api/RouteCalculator/calculate",
          {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
            },
            body: JSON.stringify(requestBody),
          }
        );

        console.log("Request trimis către backend:", requestBody);

        if (!response.ok) {
          const errorData = await response.json();
          console.error("Eroare de la backend:", errorData);
          throw new Error(errorData.error || "Eroare la calcularea rutei");
        }

        const data = await response.json();
        setRouteResult(data);
      } catch (error) {
        console.error("Eroare:", error);
        setError("Nu s-a putut calcula ruta. Vă rugăm să încercați din nou.");
      } finally {
        setLoading(false);
      }
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    // Reset expanded attractions view la începutul unei noi căutări
    setShowExpandedAttractions(false);

    // Verificăm dacă avem locații sau coordonate
    const hasStartData = startLocation || startCoordinates;
    const hasEndData = endLocation || endCoordinates;

    if (!hasStartData || !hasEndData) {
      setError(
        "Vă rugăm să introduceți ambele locații sau să le selectați pe hartă"
      );
      return;
    }
    if (!consumption || parseFloat(consumption) <= 0) {
      setError("Vă rugăm să introduceți un consum valid");
      return;
    }
    if (manualPrice && (!fuelPrice || parseFloat(fuelPrice) <= 0)) {
      setError("Vă rugăm să introduceți un preț valid");
      return;
    }

    setLoading(true);
    setError(null);
    setRouteResult(null);

    try {
      const requestBody = {
        Preference: routePreference,
        FuelType: fuelType,
        FuelConsumption: parseFloat(consumption),
      };

      // Adăugăm locația sau coordonatele de start
      if (startCoordinates) {
        requestBody.StartCoordinates = [
          startCoordinates[1],
          startCoordinates[0],
        ]; // [lon, lat]
      } else {
        requestBody.StartLocation = normalizeLocation(startLocation);
      }

      // Adăugăm locația sau coordonatele de destinație
      if (endCoordinates) {
        requestBody.EndCoordinates = [endCoordinates[1], endCoordinates[0]]; // [lon, lat]
      } else {
        requestBody.EndLocation = normalizeLocation(endLocation);
      }

      console.log(
        "🚗 Request trimis către endpoint-ul inteligent:",
        requestBody
      );

      // Validăm datele înainte de trimitere
      if (!requestBody.StartLocation && !requestBody.StartCoordinates) {
        throw new Error("Lipsește locația de start");
      }
      if (!requestBody.EndLocation && !requestBody.EndCoordinates) {
        throw new Error("Lipsește locația de destinație");
      }

      // Folosim serviciul actualizat
      const data = await fuelPriceService.calculateRouteWithFuel(requestBody);

      console.log("✅ Răspuns primit:", data);

      // Adaptăm răspunsul pentru interfața existentă
      const adaptedResult = {
        distance: data.route.distance,
        duration: data.route.duration,
        geometry: data.route.geometry,
        fuelCalculation: data.fuelCalculation,
        smartRouting: data.fuelCalculation.smart_routing,
        priceSource: data.fuelCalculation.price_source,
        recommendations: data.recommendations,
      };

      setRouteResult(adaptedResult);
    } catch (error) {
      console.error("❌ Eroare cu endpoint-ul inteligent:", error);

      // Fallback la metoda tradițională
      console.log("🔄 Încercăm metoda tradițională...");
      try {
        const fallbackRequestBody = {
          Preference: routePreference,
        };

        if (startCoordinates) {
          fallbackRequestBody.StartCoordinates = [
            startCoordinates[1],
            startCoordinates[0],
          ];
        } else {
          fallbackRequestBody.StartLocation = normalizeLocation(startLocation);
        }

        if (endCoordinates) {
          fallbackRequestBody.EndCoordinates = [
            endCoordinates[1],
            endCoordinates[0],
          ];
        } else {
          fallbackRequestBody.EndLocation = normalizeLocation(endLocation);
        }

        const fallbackData = await fuelPriceService.calculateBasicRoute(
          fallbackRequestBody
        );
        setRouteResult(fallbackData);
        console.log("✅ Fallback reușit");
      } catch (fallbackError) {
        console.error("❌ Eroare și cu fallback:", fallbackError);
        setError("Nu s-a putut calcula ruta. Vă rugăm să încercați din nou.");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleSearchAttractions = async () => {
    if (!selectedAttraction || !radius || !routeResult) {
      setError(
        "Vă rugăm să selectați o atracție și să introduceți raza de căutare"
      );
      return;
    }

    setLoading(true);
    try {
      // Convertim raza din km în metri
      const radiusInMeters = parseFloat(radius) * 1000;

      // Folosim valoarea mapată pentru kinds
      const mappedKind = attractionTypeMap[selectedAttraction];

      let url;
      if (endCoordinates) {
        // Dacă avem coordonate, le folosim direct
        url = `http://localhost:5283/api/TouristAttractions/nearbyZone?lat=${endCoordinates[0]}&lon=${endCoordinates[1]}&radius=${radiusInMeters}&kinds=${mappedKind}`;
      } else {
        // Altfel folosim numele locației
        url = `http://localhost:5283/api/TouristAttractions/nearbyZone?location=${encodeURIComponent(
          endLocation
        )}&radius=${radiusInMeters}&kinds=${mappedKind}`;
      }

      console.log("URL request:", url);

      const response = await fetch(url, {
        method: "GET",
        headers: {
          "Content-Type": "application/json",
        },
      });
      console.log("Response status:", response.status);
      console.log("Mapped kind:", mappedKind);
      console.log("Radius in meters:", radiusInMeters);
      console.log("End location/coordinates:", endCoordinates || endLocation);

      if (!response.ok) {
        const errorText = await response.text();
        console.error("Error response:", errorText);
        throw new Error(
          `Eroare la căutarea atracțiilor: ${response.status} ${response.statusText}`
        );
      }

      const data = await response.json();
      console.log("Atracții găsite:", data);
      setFoundLocations(data);
    } catch (error) {
      console.error("Eroare detaliată:", error);
      setError(
        error.message ||
          "Nu s-au putut găsi atracțiile. Vă rugăm să încercați din nou."
      );
    } finally {
      setLoading(false);
    }
  };

  const consumptionResult = calculateFuelConsumption();

  const formatDuration = (hours, minutes) => {
    const totalMinutes = minutes;
    const formattedHours = Math.floor(totalMinutes / 60);
    const formattedMinutes = totalMinutes % 60;

    if (formattedHours === 0) {
      return `${formattedMinutes} minute`;
    } else if (formattedMinutes === 0) {
      return `${formattedHours} ${formattedHours === 1 ? "oră" : "ore"}`;
    } else {
      return `${formattedHours} ${
        formattedHours === 1 ? "oră" : "ore"
      } și ${formattedMinutes} ${formattedMinutes === 1 ? "minut" : "minute"}`;
    }
  };

  const handleAttractionClick = (attraction) => {
    if (selectedAttraction === attraction) {
      setSelectedAttraction(null);
    } else {
      setSelectedAttraction(attraction);
    }
  };

  const handleLocationSelect = (coords, type) => {
    if (type === "start") {
      setStartCoordinates(coords);
      setStartLocation(
        `📍 Coordonate: ${coords[0].toFixed(4)}, ${coords[1].toFixed(4)}`
      );
    } else if (type === "end") {
      setEndCoordinates(coords);
      setEndLocation(
        `📍 Coordonate: ${coords[0].toFixed(4)}, ${coords[1].toFixed(4)}`
      );
    }

    // Oprim modul de selecție
    setIsSelectingLocation(false);
    setSelectionType(null);
  };

  const startLocationSelection = (type) => {
    setIsSelectingLocation(true);
    setSelectionType(type);
  };

  const showAttractionOnMap = (location) => {
    if (location.coordinates) {
      console.log(
        "Afișez atracția pe hartă:",
        location.name,
        location.coordinates
      );
      // Setez coordonatele pentru a centra harta pe această atracție
      setSelectedAttractionCoords([
        location.coordinates[0], // latitude
        location.coordinates[1], // longitude
      ]);
    }
  };

  return (
    <div className="container">
      <div className="home-container">
        <div className="header-section">
          <h1 className="tt-title">Travel Thing (DEMO)</h1>
          <p className="tt-subtitle">
            Calculator de călătorii și atracții turistice
          </p>
          {loading && <div className="tt-loading">Se încarcă prețurile...</div>}
          {error && <div className="tt-error">{error}</div>}
        </div>

        <div className="main-content">
          {/* Coloana stângă - Formular */}
          <div className="form-section">
            <form className="tt-form" onSubmit={handleSubmit}>
              <div className="form-group">
                <h3>Date rută</h3>
                <div className="location-inputs">
                  <div className="location-input-wrapper">
                    <Input
                      type="text"
                      placeholder="Introdu punctul de plecare..."
                      value={startLocation}
                      onChange={(e) => {
                        setStartLocation(e.target.value);
                        setStartCoordinates(null);
                      }}
                      onBlur={(e) => {
                        if (!startCoordinates) {
                          setStartLocation(normalizeLocation(e.target.value));
                        }
                      }}
                    />
                    <button
                      type="button"
                      className="map-picker-btn"
                      onClick={() => startLocationSelection("start")}
                      title="Selectează pe hartă"
                    >
                      📍
                    </button>
                  </div>
                  <div className="location-input-wrapper">
                    <Input
                      type="text"
                      placeholder="Introdu punctul de sosire..."
                      value={endLocation}
                      onChange={(e) => {
                        setEndLocation(e.target.value);
                        setEndCoordinates(null);
                      }}
                      onBlur={(e) => {
                        if (!endCoordinates) {
                          setEndLocation(normalizeLocation(e.target.value));
                        }
                      }}
                    />
                    <button
                      type="button"
                      className="map-picker-btn"
                      onClick={() => startLocationSelection("end")}
                      title="Selectează pe hartă"
                    >
                      📍
                    </button>
                  </div>
                </div>

                <div className="fuel-inputs">
                  <Input
                    type="number"
                    placeholder="Introdu consumul mediu (L/100km)"
                    value={consumption}
                    onChange={(e) => setConsumption(e.target.value)}
                  />
                  <select
                    className="tt-select"
                    value={fuelType}
                    onChange={(e) => setFuelType(e.target.value)}
                  >
                    {fuelTypes.map((type) => (
                      <option key={type} value={type}>
                        {type}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="route-preferences">
                  {routePreferences.map((pref) => (
                    <button
                      key={pref.value}
                      type="button"
                      className={`tt-route-btn ${
                        routePreference === pref.value ? "active" : ""
                      }`}
                      onClick={() => handleRoutePreferenceChange(pref.value)}
                    >
                      <span className="tt-route-icon">{pref.icon}</span>
                      {pref.label}
                    </button>
                  ))}
                </div>

                <div className="price-settings">
                  <div className="price-toggle">
                    <span className="toggle-label">Preț automat</span>
                    <label className="toggle-switch">
                      <input
                        type="checkbox"
                        checked={manualPrice}
                        onChange={(e) => setManualPrice(e.target.checked)}
                      />
                      <span className="slider"></span>
                    </label>
                    <span className="toggle-label">Preț manual</span>
                  </div>
                  {manualPrice ? (
                    <Input
                      type="number"
                      placeholder="Introdu prețul carburantului (RON/L)"
                      value={fuelPrice}
                      onChange={(e) => setFuelPrice(e.target.value)}
                    />
                  ) : (
                    <div className="tt-auto-price">
                      <span className="desktop-price">
                        Preț automat pentru {fuelType}:{" "}
                        {fuelPrices[fuelType] || "Se încarcă..."} RON/L
                      </span>
                      <span className="mobile-price">
                        Preț automat: {fuelPrices[fuelType] || "..."} RON/L
                      </span>
                    </div>
                  )}
                </div>

                <div className="checkbox-options">
                  <div className="modern-checkbox-group">
                    <label className="modern-checkbox">
                      <input
                        type="checkbox"
                        checked={isRoundTrip}
                        onChange={(e) => setIsRoundTrip(e.target.checked)}
                      />
                      <span className="checkmark"></span>
                      <span className="checkbox-text">Include dus-întors</span>
                    </label>
                  </div>

                  <div className="modern-checkbox-group">
                    <label className="modern-checkbox">
                      <input
                        type="checkbox"
                        checked={showAttractions}
                        onChange={(e) => setShowAttractions(e.target.checked)}
                      />
                      <span className="checkmark"></span>
                      <span className="checkbox-text">
                        Caută atracții turistice
                      </span>
                    </label>

                    {/* Slider pentru raza de căutare */}
                    {showAttractions && (
                      <div className="attractions-count-slider">
                        <label className="slider-label">
                          Raza de căutare: <strong>{radius || 5}km</strong>
                        </label>
                        <div className="range-slider-wrapper">
                          <input
                            type="range"
                            min="0.5"
                            max="10"
                            step="0.5"
                            value={radius || 5}
                            onChange={(e) => setRadius(e.target.value)}
                            className="range-slider"
                          />
                          <div className="range-labels">
                            <span>0.5km</span>
                            <span>10km</span>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                </div>

                <Button type="submit" disabled={loading} className="tt-btn">
                  {loading ? "Se calculează..." : "Calculează ruta"}
                </Button>
              </div>

              {/* Secțiunea pentru atracții turistice */}
              {showAttractions && routeResult && (
                <div className="form-group attractions-form-group">
                  <h3>Atracții turistice</h3>

                  <div className="attraction-types">
                    {[
                      {
                        key: "restaurants",
                        label: "Restaurante",
                        icon: cutleryIcon,
                      },
                      { key: "cafes", label: "Cafenele", icon: cafeIcon },
                      { key: "parks", label: "Parcuri", icon: parkIcon },
                      { key: "museums", label: "Muzee", icon: museumIcon },
                      { key: "theaters", label: "Teatre", icon: theaterIcon },
                      { key: "shops", label: "Magazine", icon: shopIcon },
                      { key: "churches", label: "Biserici", icon: churchIcon },
                      {
                        key: "architecture",
                        label: "Arhitectură",
                        icon: architectureIcon,
                      },
                      {
                        key: "monuments",
                        label: "Monumente",
                        icon: monumentIcon,
                      },
                      { key: "castles", label: "Cetăți", icon: castleIcon },
                      { key: "lakes", label: "Cazări", icon: lakeIcon },
                      {
                        key: "beaches",
                        label: "Alte locații",
                        icon: beachIcon,
                      },
                    ].map(({ key, label, icon }) => (
                      <div
                        key={key}
                        className={`attraction-type ${
                          selectedAttraction === key ? "selected" : ""
                        }`}
                        onClick={() => handleAttractionClick(key)}
                      >
                        <img src={icon} alt={label} />
                        <span>{label}</span>
                      </div>
                    ))}
                  </div>

                  <div className="attractions-buttons">
                    <Button
                      type="button"
                      disabled={loading || !selectedAttraction || !radius}
                      onClick={handleSearchAttractions}
                      className="tt-btn"
                    >
                      {loading ? "Se caută..." : "Caută atracții"}
                    </Button>

                    {foundLocations.length > 0 && (
                      <Button
                        type="button"
                        onClick={() => {
                          setFoundLocations([]);
                          setSelectedAttraction(null);
                          setSelectedAttractionCoords(null);
                        }}
                        className="tt-btn cancel-btn"
                      >
                        ✕ Anulează căutarea
                      </Button>
                    )}
                  </div>
                </div>
              )}
            </form>

            {/* Secțiunea expandată de atracții */}
            {foundLocations.length > 0 && (
              <div className="tourist-attractions">
                <div className="attractions-header">
                  <h3>Atracții găsite ({foundLocations.length})</h3>
                </div>
                <div className="attractions-grid">
                  {foundLocations.map((location, index) => (
                    <div key={index} className="attraction-card">
                      <h4>{location.name}</h4>
                      {location.description && (
                        <div className="attraction-description">
                          {location.description}
                        </div>
                      )}
                      <div className="attraction-distance">
                        <strong>Distanță de la rută:</strong>{" "}
                        {location.distance
                          ? location.distance < 1000
                            ? `${Math.round(location.distance * 1000)}m`
                            : `${(location.distance / 1000).toFixed(1)}km`
                          : "Necunoscută"}
                      </div>
                      <div className="attraction-buttons">
                        {location.coordinates && (
                          <button
                            onClick={() => showAttractionOnMap(location)}
                            className="show-on-map-btn"
                            style={{
                              background: "#4caf50",
                              color: "white",
                              border: "none",
                              padding: "0.5rem 0.75rem",
                              borderRadius: "6px",
                            }}
                          >
                            📍 Arată pe hartă
                          </button>
                        )}
                        <a
                          href={
                            location.coordinates
                              ? `https://www.google.com/maps/place/${location.coordinates[0]},${location.coordinates[1]}/@${location.coordinates[0]},${location.coordinates[1]},17z`
                              : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(
                                  location.name
                                )}`
                          }
                          target="_blank"
                          rel="noopener noreferrer"
                          className="maps-link"
                        >
                          🗺️ Arată-mi pe Maps
                        </a>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Coloana dreaptă - Hartă și rezultate */}
          <div className="results-section">
            <div className="combined-container">
              {/* Harta */}
              <div className="map-section">
                <TravelMap
                  geometry={routeResult?.geometry}
                  startLocation={startLocation}
                  endLocation={endLocation}
                  isSelectingLocation={isSelectingLocation}
                  selectionType={selectionType}
                  onLocationSelect={handleLocationSelect}
                  startCoordinates={startCoordinates}
                  endCoordinates={endCoordinates}
                  selectedAttractionCoords={selectedAttractionCoords}
                  foundLocations={foundLocations}
                />
              </div>

              {/* Detalii */}
              <div className="details-section">
                {routeResult && (
                  <div className="route-results">
                    <h3>Rezultate rută</h3>
                    <div className="result-details">
                      <div className="result-item">
                        <strong>Distanță:</strong>{" "}
                        {routeResult.distance.kilometers *
                          (isRoundTrip ? 2 : 1)}{" "}
                        km
                      </div>
                      <div className="result-item">
                        <strong>Durată:</strong>{" "}
                        {formatDuration(
                          routeResult.duration.hours * (isRoundTrip ? 2 : 1),
                          routeResult.duration.minutes * (isRoundTrip ? 2 : 1)
                        )}
                      </div>

                      {consumptionResult.error ? (
                        <div className="tt-warning">
                          {consumptionResult.error}
                        </div>
                      ) : (
                        <>
                          <div className="result-item">
                            <strong>Consum carburant:</strong>{" "}
                            {consumptionResult.fuelNeeded} L
                          </div>
                          <div className="result-item">
                            <strong>Cost total:</strong>{" "}
                            {consumptionResult.totalCost} RON
                          </div>

                          <div className="split-cost-section">
                            <label className="checkbox-label">
                              <input
                                type="checkbox"
                                checked={showSplitCost}
                                onChange={(e) =>
                                  setShowSplitCost(e.target.checked)
                                }
                              />
                              Împarte costul cu prietenii
                            </label>
                            {showSplitCost && (
                              <div className="split-cost-inputs">
                                <label htmlFor="numberOfPeople">
                                  Numărul de persoane:
                                </label>
                                <input
                                  type="number"
                                  id="numberOfPeople"
                                  min="1"
                                  value={numberOfPeople}
                                  onChange={(e) =>
                                    setNumberOfPeople(
                                      Math.max(1, parseInt(e.target.value) || 1)
                                    )
                                  }
                                  className="number-input"
                                />
                                <div className="split-result">
                                  <strong>Cost per persoană:</strong>{" "}
                                  {(
                                    consumptionResult.totalCost / numberOfPeople
                                  ).toFixed(2)}{" "}
                                  RON
                                </div>
                              </div>
                            )}
                          </div>
                        </>
                      )}

                      {foundLocations.length > 0 && (
                        <div className="attractions-in-results">
                          <h4>Atracții găsite ({foundLocations.length})</h4>
                          <div className="attractions-compact">
                            {foundLocations
                              .slice(0, 3)
                              .map((location, index) => (
                                <div key={index} className="attraction-compact">
                                  <strong>{location.name}</strong>
                                  <span className="distance-compact">
                                    {location.distance
                                      ? location.distance < 1000
                                        ? `${Math.round(
                                            location.distance * 1000
                                          )}m`
                                        : `${(location.distance / 1000).toFixed(
                                            1
                                          )}km`
                                      : "?"}
                                  </span>
                                </div>
                              ))}
                            {foundLocations.length > 3 && (
                              <div
                                className="more-attractions clickable"
                                onClick={() => setShowExpandedAttractions(true)}
                              >
                                +{foundLocations.length - 3} mai multe
                              </div>
                            )}
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
