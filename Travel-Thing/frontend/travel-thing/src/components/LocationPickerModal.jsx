import React, { useState, useRef, useEffect } from "react";
import { MapContainer, TileLayer, Marker, useMapEvents } from "react-leaflet";
import L from "leaflet";
import "./LocationPickerModal.css";

// Configurare marker personalizat
const customIcon = new L.Icon({
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  iconRetinaUrl:
    "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  shadowSize: [41, 41],
});

function LocationPicker({ onLocationSelect, selectedLocation }) {
  const [markerPosition, setMarkerPosition] = useState(
    selectedLocation || null
  );

  useMapEvents({
    click(e) {
      const newPosition = [e.latlng.lat, e.latlng.lng];
      setMarkerPosition(newPosition);
      onLocationSelect(newPosition);
    },
  });

  return markerPosition ? (
    <Marker position={markerPosition} icon={customIcon} />
  ) : null;
}

export default function LocationPickerModal({
  isOpen,
  onClose,
  onLocationSelect,
  title = "Selectează locația",
  initialLocation = null,
  existingLocation = null, // coordonatele existente, dacă există
}) {
  const [selectedLocation, setSelectedLocation] = useState(existingLocation);
  const mapRef = useRef(null);

  useEffect(() => {
    if (existingLocation && mapRef.current) {
      mapRef.current.setView(existingLocation, 13);
    }
  }, [existingLocation]);

  if (!isOpen) return null;

  const handleLocationSelect = (location) => {
    setSelectedLocation(location);
  };

  const handleConfirm = () => {
    if (selectedLocation) {
      onLocationSelect(selectedLocation);
      onClose();
    }
  };

  const handleClose = () => {
    setSelectedLocation(null);
    onClose();
  };

  // Centru implicit: București
  const defaultCenter = [44.4268, 26.1025];
  const center = existingLocation || initialLocation || defaultCenter;

  return (
    <div className="location-picker-overlay">
      <div className="location-picker-modal">
        <div className="location-picker-header">
          <h2>{title}</h2>
          <button className="close-button" onClick={handleClose}>
            ×
          </button>
        </div>

        <div className="location-picker-content">
          <p className="location-picker-info">
            Faceți click pe hartă pentru a selecta locația dorită
          </p>

          <div className="map-container">
            <MapContainer
              center={center}
              zoom={existingLocation ? 13 : 7}
              style={{ height: "100%", width: "100%" }}
              ref={mapRef}
            >
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              <LocationPicker
                onLocationSelect={handleLocationSelect}
                selectedLocation={existingLocation}
              />
            </MapContainer>
          </div>

          {selectedLocation && (
            <div className="selected-coordinates">
              <p>Coordonate selectate:</p>
              <p>Latitudine: {selectedLocation[0].toFixed(6)}</p>
              <p>Longitudine: {selectedLocation[1].toFixed(6)}</p>
            </div>
          )}
        </div>

        <div className="location-picker-footer">
          <button className="cancel-button" onClick={handleClose}>
            Anulează
          </button>
          <button
            className="confirm-button"
            onClick={handleConfirm}
            disabled={!selectedLocation}
          >
            Confirmă locația
          </button>
        </div>
      </div>
    </div>
  );
}
