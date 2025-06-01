import React, { useState, useEffect } from "react";
import Button from "../components/Button";
import Input from "../components/Input";
import "./Home.css";
import { fuelPriceService } from "../services/fuelPriceService";
import AverageFuelPrices from "../components/AverageFuelPrices";
import TravelMap from "../components/map/map";
import Divider from "../components/Divider";
import LocationPickerModal from "../components/LocationPickerModal";

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
  const [showStartPicker, setShowStartPicker] = useState(false);
  const [showEndPicker, setShowEndPicker] = useState(false);
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
        preference: routePreference,
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
      setShowAttractions(true);
    } catch (error) {
      console.error("Eroare:", error);
      setError("Nu s-a putut calcula ruta. Vă rugăm să încercați din nou.");
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

  const handleStartLocationSelect = (coords) => {
    setStartCoordinates(coords);
    setStartLocation(
      `📍 Coordonate: ${coords[0].toFixed(4)}, ${coords[1].toFixed(4)}`
    );
  };

  const handleEndLocationSelect = (coords) => {
    setEndCoordinates(coords);
    setEndLocation(
      `📍 Coordonate: ${coords[0].toFixed(4)}, ${coords[1].toFixed(4)}`
    );
  };

  return (
    <>
      <div className="home-container">
        <h1 className="tt-title">Travel Thing (DEMO)</h1>
        <p className="tt-subtitle">
          Calculator de călătorii și atracții turistice
        </p>
        {loading && <div className="tt-loading">Se încarcă prețurile...</div>}
        {error && <div className="tt-error">{error}</div>}
        <form className="tt-form" onSubmit={handleSubmit}>
          <div className="tt-row">
            <div className="location-input-wrapper">
              <Input
                type="text"
                placeholder="Introdu punctul de plecare..."
                value={startLocation}
                onChange={(e) => {
                  setStartLocation(e.target.value);
                  setStartCoordinates(null); // Resetăm coordonatele când se modifică textul
                }}
                onBlur={(e) => {
                  if (!startCoordinates) {
                    // Normalizăm doar dacă nu sunt coordonate setate
                    setStartLocation(normalizeLocation(e.target.value));
                  }
                }}
              />
              <button
                type="button"
                className="map-picker-btn"
                onClick={() => setShowStartPicker(true)}
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
                  setEndCoordinates(null); // Resetăm coordonatele când se modifică textul
                }}
                onBlur={(e) => {
                  if (!endCoordinates) {
                    // Normalizăm doar dacă nu sunt coordonate setate
                    setEndLocation(normalizeLocation(e.target.value));
                  }
                }}
              />
              <button
                type="button"
                className="map-picker-btn"
                onClick={() => setShowEndPicker(true)}
                title="Selectează pe hartă"
              >
                📍
              </button>
            </div>
          </div>
          <div className="tt-row">
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
          <div className="tt-route-preferences">
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
          <div className="tt-form-group">
            <Button type="button" onClick={() => setManualPrice(!manualPrice)}>
              {manualPrice ? "Preț manual" : "Preț automat"}
            </Button>
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
            <div className="tt-form-group-checkbox">
              <p>Include dus-întors</p>
              <input
                type="checkbox"
                checked={isRoundTrip}
                onChange={(e) => setIsRoundTrip(e.target.checked)}
              />
            </div>
          </div>

          <Button type="submit" disabled={loading}>
            {loading ? "Se calculează..." : "Calculează"}
          </Button>
        </form>

        {routeResult && (
          <div className="tt-result">
            <h3>Rezultate ruta:</h3>
            <div className="tt-result-details">
              <p>
                Distanță:{" "}
                {routeResult.distance.kilometers * (isRoundTrip ? 2 : 1)} km
              </p>
              <p>
                Durată:{" "}
                {formatDuration(
                  routeResult.duration.hours * (isRoundTrip ? 2 : 1),
                  routeResult.duration.minutes * (isRoundTrip ? 2 : 1)
                )}
              </p>
              {consumptionResult.error ? (
                <p className="tt-warning">{consumptionResult.error}</p>
              ) : (
                <>
                  <p>Consum carburant: {consumptionResult.fuelNeeded} L</p>
                  <p>Cost carburant total: {consumptionResult.totalCost} RON</p>
                  <div className="tt-split-option">
                    <label className="tt-checkbox-label">
                      <input
                        type="checkbox"
                        checked={showSplitCost}
                        onChange={(e) => setShowSplitCost(e.target.checked)}
                      />
                      Împarte costul cu prietenii
                    </label>
                  </div>
                  {showSplitCost && (
                    <div className="tt-split-cost">
                      <div className="tt-split-input">
                        <label htmlFor="numberOfPeople">
                          Împarte costul între:
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
                          className="tt-number-input"
                        />
                        <span>persoane</span>
                      </div>
                      <p className="tt-split-result">
                        Cost per persoană:{" "}
                        {(consumptionResult.totalCost / numberOfPeople).toFixed(
                          2
                        )}{" "}
                        RON
                      </p>
                    </div>
                  )}
                </>
              )}
            </div>
            <Divider />
            <TravelMap
              geometry={routeResult.geometry}
              startLocation={startLocation}
              endLocation={endLocation}
            />
          </div>
        )}

        {showAttractions && (
          <>
            <Divider />
            <div className="tt-attractions">
              <h3>Cautare atracții turistice</h3>
              <Input
                min="0.1"
                max="10"
                step="0.1"
                type="number"
                className="tt-attractions-form-input"
                placeholder="Raza de cautare (km | ex 0.5)"
                value={radius}
                onChange={(e) => setRadius(e.target.value)}
              />
              <div className="tt-attractions-form">
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "restaurants"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("restaurants")}
                >
                  <p>Restaurante</p>
                  <img src={cutleryIcon} alt="Restaurante" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "cafes"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("cafes")}
                >
                  <p>Cafenele</p>
                  <img src={cafeIcon} alt="Cafenele" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "parks"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("parks")}
                >
                  <p>Parcuri</p>
                  <img src={parkIcon} alt="Parcuri" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "museums"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("museums")}
                >
                  <p>Muzee</p>
                  <img src={museumIcon} alt="Muzee" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "theaters"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("theaters")}
                >
                  <p>Teatre și divertisment</p>
                  <img src={theaterIcon} alt="Teatre și divertisment" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "shops"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("shops")}
                >
                  <p>Magazine</p>
                  <img src={shopIcon} alt="Magazine" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "churches"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("churches")}
                >
                  <p>Biserici</p>
                  <img src={churchIcon} alt="Biserici" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "architecture"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("architecture")}
                >
                  <p>Arhitectură</p>
                  <img src={architectureIcon} alt="Arhitectură" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "monuments"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("monuments")}
                >
                  <p>Monumente</p>
                  <img src={monumentIcon} alt="Monumente" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "castles"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("castles")}
                >
                  <p>Cetați</p>
                  <img src={castleIcon} alt="Cetați" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "lakes"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("lakes")}
                >
                  <p>Cazări</p>
                  <img src={lakeIcon} alt="Cazări" />
                </div>
                <div
                  className={`tt-attractions-form-image ${
                    selectedAttraction === "beaches"
                      ? "selected"
                      : selectedAttraction
                      ? "disabled"
                      : ""
                  }`}
                  onClick={() => handleAttractionClick("beaches")}
                >
                  <p>Alte locații</p>
                  <img src={beachIcon} alt="Alte locații" />
                </div>
              </div>
              <Button
                type="button"
                disabled={loading}
                className="tt-attractions-form-button"
                onClick={handleSearchAttractions}
              >
                {loading ? "Se caută..." : "Caută"}
              </Button>
            </div>
          </>
        )}

        {foundLocations.length > 0 && (
          <div className="locations-container">
            <h2>Locații găsite</h2>
            <div className="locations-grid">
              {foundLocations.map((location, index) => (
                <div key={index} className="location-card">
                  <h3>{location.name}</h3>
                  <p className="location-description">
                    {location.description ||
                      "Momentan nu avem informații despre această locație."}
                  </p>
                  <p className="location-distance">
                    Distanță:{" "}
                    {location.distance
                      ? location.distance < 1000
                        ? `${Math.round(location.distance * 1000)} metri`
                        : `${(location.distance / 1000).toFixed(2)} km`
                      : "Necunoscută"}
                  </p>
                  {location.mapsUrl && (
                    <a
                      href={location.mapsUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="location-link"
                    >
                      Vezi pe Google Maps
                    </a>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      <LocationPickerModal
        isOpen={showStartPicker}
        onClose={() => setShowStartPicker(false)}
        onLocationSelect={handleStartLocationSelect}
        title="Selectează punctul de plecare"
        existingLocation={startCoordinates}
      />

      <LocationPickerModal
        isOpen={showEndPicker}
        onClose={() => setShowEndPicker(false)}
        onLocationSelect={handleEndLocationSelect}
        title="Selectează punctul de sosire"
        existingLocation={endCoordinates}
      />
    </>
  );
}
