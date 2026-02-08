# TravelThing 🚗🗺️

**TravelThing** este o aplicație web care îți estimează costul transportului auto între două locații din România, luând în considerare:
- distanța parcursă
- consumul mediu al vehiculului
- prețurile actuale ale carburanților

Aplicația oferă și sugestii de obiective turistice în zona de destinație.

---

## 🧠 Funcționalități principale

- Introducerea locației de plecare și destinației
- Estimarea rutei cu distanță și durată (folosind OpenRouteService API)
- Calcularea costului călătoriei în funcție de:
  - consum mediu (litri/100km)
  - tipul carburantului
  - prețuri actualizate prin web scraping
- Suport pentru vehicule electrice (stații de încărcare din OpenChargeMap)
- Afișarea de obiective turistice în zona destinației
- Interfață prietenoasă și responsive

---

## 🛠️ Stack tehnologic

- **Frontend**: React (Vite), TailwindCSS, Fetch API
- **Backend**: ASP.NET Core Web API, Entity Framework Core, SQLite (dev) / SQL Server (prod)
- **Third-party APIs**:
  - [OpenRouteService](https://openrouteservice.org/)
  - [OpenChargeMap](https://openchargemap.org/)
  - Web scraping prețuri carburanți (Petrom, OMV, MOL etc.)

  ## IMAGE: 
  <img width="1790" height="884" alt="image" src="https://github.com/user-attachments/assets/ea9ab255-09f8-444c-bbaf-c341addd46b1" />


