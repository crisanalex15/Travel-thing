import React, { useEffect, useState } from "react";
import {
  MapContainer,
  TileLayer,
  Marker,
  Popup,
  Polyline,
  useMapEvents,
} from "react-leaflet";
import L from "leaflet";
import icon from "leaflet/dist/images/marker-icon.png";
import iconShadow from "leaflet/dist/images/marker-shadow.png";
import { decode } from "@mapbox/polyline";
import "./CustomMap.css";

// Fix default marker icon
let DefaultIcon = L.icon({
  iconUrl: icon,
  shadowUrl: iconShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  className: "custom-marker",
});

L.Marker.prototype.options.icon = DefaultIcon;

// Componenta pentru gestionarea click-urilor pe hartă
function MapClickHandler({
  isSelectingLocation,
  selectionType,
  onLocationSelect,
}) {
  useMapEvents({
    click(e) {
      if (isSelectingLocation) {
        const coords = [e.latlng.lat, e.latlng.lng];
        onLocationSelect(coords, selectionType);
      }
    },
  });
  return null;
}

// Funcție pentru calcularea zoom-ului în funcție de distanță
const calculateZoom = (coordinates) => {
  if (!coordinates || coordinates.length < 2) return 8;

  // Calculăm distanța în grade între punctele extreme
  const latDiff = Math.abs(
    coordinates[0][0] - coordinates[coordinates.length - 1][0]
  );
  const lonDiff = Math.abs(
    coordinates[0][1] - coordinates[coordinates.length - 1][1]
  );
  const maxDiff = Math.max(latDiff, lonDiff);

  // Mapăm distanța la un nivel de zoom potrivit
  if (maxDiff > 15) return 2; // Pentru distanțe intercontinentale
  if (maxDiff > 10) return 3; // Pentru distanțe între țări mari
  if (maxDiff > 7) return 4; // Pentru distanțe între țări mici
  if (maxDiff > 4) return 5; // Pentru distanțe între regiuni mari
  if (maxDiff > 2) return 6; // Pentru distanțe între regiuni mici
  if (maxDiff > 1) return 7; // Pentru distanțe între orașe mari
  if (maxDiff > 0.5) return 8; // Pentru distanțe între orașe mici
  if (maxDiff > 0.2) return 9; // Pentru distanțe între cartiere
  if (maxDiff > 0.1) return 10; // Pentru distanțe între străzi
  return 13; // Pentru distanțe foarte mici (în aceeași zonă)
};

export default function TravelMap({
  geometry,
  startLocation,
  endLocation,
  isSelectingLocation = false,
  selectionType = null,
  onLocationSelect = null,
  startCoordinates = null,
  endCoordinates = null,
  selectedAttractionCoords = null,
  foundLocations = [],
}) {
  // Setez iconurile o singură dată, global
  if (!window.leafletIconsFixed) {
    try {
      if (L && L.Icon && L.Icon.Default) {
        delete L.Icon.Default.prototype._getIconUrl;
        L.Icon.Default.mergeOptions({
          iconUrl: icon,
          iconRetinaUrl: icon,
          shadowUrl: iconShadow,
        });
        window.leafletIconsFixed = true;
      }
    } catch (error) {
      console.error("Eroare la setarea iconurilor:", error);
    }
  }

  // Decode polyline to coordinates
  const coordinates = geometry
    ? decode(geometry).map((coord) => [coord[0], coord[1]])
    : [];

  // Setările pentru România când nu există o rută
  const romaniaCenter = [45.9442, 25.0094]; // Centrul României (aproximativ Brașov)
  const romaniaZoom = 7; // Zoom potrivit pentru a vedea toată România

  // Calculate center point based on priority:
  // 1. Selected attraction coordinates (if any)
  // 2. Route center (if exists)
  // 3. Romania center (default)
  const center = selectedAttractionCoords
    ? selectedAttractionCoords
    : coordinates.length > 0
    ? [
        (coordinates[0][0] + coordinates[coordinates.length - 1][0]) / 2,
        (coordinates[0][1] + coordinates[coordinates.length - 1][1]) / 2,
      ]
    : romaniaCenter;

  // Calculate zoom based on priority:
  // 1. Higher zoom for selected attraction
  // 2. Route zoom (if exists)
  // 3. Romania zoom (default)
  const zoom = selectedAttractionCoords
    ? 15 // High zoom for attraction
    : coordinates.length > 0
    ? calculateZoom(coordinates)
    : romaniaZoom;

  // Fix pentru redimensionarea hărții
  useEffect(() => {
    const timer = setTimeout(() => {
      const mapElements = document.querySelectorAll(".leaflet-container");
      mapElements.forEach((element) => {
        if (element._leaflet_map) {
          element._leaflet_map.invalidateSize();
        }
      });
    }, 200);

    return () => clearTimeout(timer);
  }, [geometry, startCoordinates, endCoordinates, selectedAttractionCoords]);

  return (
    <div style={{ position: "relative" }}>
      {isSelectingLocation && (
        <div
          style={{
            position: "absolute",
            top: "10px",
            left: "10px",
            background: "#4caf50",
            color: "white",
            padding: "8px 12px",
            borderRadius: "4px",
            zIndex: 1000,
            fontSize: "14px",
            fontWeight: "500",
          }}
        >
          {selectionType === "start"
            ? "Selectează punctul de plecare"
            : "Selectează punctul de sosire"}
        </div>
      )}
      <MapContainer
        center={center}
        zoom={zoom}
        scrollWheelZoom={true}
        style={{
          height: "100%",
          width: "100%",
          minHeight: "700px",
          border: isSelectingLocation
            ? "3px solid #ff9800"
            : "3px solid #4caf50",
          borderRadius: "16px",
          boxShadow: "0 8px 16px rgba(0,0,0,0.2)",
          overflow: "hidden",
          cursor: isSelectingLocation ? "crosshair" : "grab",
        }}
        whenCreated={(mapInstance) => {
          // Forțează redimensionarea corectă a hărții
          setTimeout(() => {
            mapInstance.invalidateSize();
          }, 100);
        }}
      >
        <TileLayer
          attribution="&copy; OpenStreetMap contributors"
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <MapClickHandler
          isSelectingLocation={isSelectingLocation}
          selectionType={selectionType}
          onLocationSelect={onLocationSelect}
        />
        {coordinates.length > 0 && (
          <>
            <Marker position={coordinates[0]}>
              <Popup>{startLocation || "Punct de plecare"}</Popup>
            </Marker>
            <Marker
              position={coordinates[coordinates.length - 1]}
              className="end-marker"
            >
              <Popup>{endLocation || "Punct de sosire"}</Popup>
            </Marker>
            <Polyline
              positions={coordinates}
              color="#4caf50"
              weight={3}
              opacity={0.7}
            />
          </>
        )}

        {/* Markerii pentru locațiile selectate manual (fără rută) */}
        {!coordinates.length && startCoordinates && (
          <Marker position={startCoordinates}>
            <Popup>{startLocation || "Punct de plecare selectat"}</Popup>
          </Marker>
        )}

        {!coordinates.length && endCoordinates && (
          <Marker position={endCoordinates}>
            <Popup>{endLocation || "Punct de sosire selectat"}</Popup>
          </Marker>
        )}

        {/* Markerii pentru atracțiile găsite */}
        {foundLocations.map((location, index) => {
          if (location.coordinates) {
            return (
              <Marker
                key={`attraction-${index}`}
                position={[location.coordinates[0], location.coordinates[1]]}
              >
                <Popup>
                  <div>
                    <strong>{location.name}</strong>
                    {location.description && (
                      <p style={{ margin: "5px 0", fontSize: "0.9rem" }}>
                        {location.description}
                      </p>
                    )}
                    <div style={{ fontSize: "0.8rem", color: "#666" }}>
                      Distanță:{" "}
                      {location.distance
                        ? location.distance < 1000
                          ? `${Math.round(location.distance * 1000)}m`
                          : `${(location.distance / 1000).toFixed(1)}km`
                        : "Necunoscută"}
                    </div>
                  </div>
                </Popup>
              </Marker>
            );
          }
          return null;
        })}
      </MapContainer>
    </div>
  );
}
