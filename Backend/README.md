# Backend – Bücher Ausleihe

ASP.NET Core 8 Web API with SQL Server.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for SQL Server)

## 1. Start the Database

From the repo root:

```bash
docker compose up -d
```

Starts SQL Server on `localhost:1433` with:
- User: `sa`
- Password: `SicheresPasswort123!`

## 2. Run the API

```bash
cd Backend/Api
dotnet run
```

Migrations and seeding run automatically on startup.

## URLs

| Profile | URL |
|---------|-----|
| HTTP    | http://localhost:5068 |
| HTTPS   | https://localhost:7213 |
| Swagger | http://localhost:5068/swagger |
| Health  | http://localhost:5068/api/health |

## Configuration

`Backend/Api/appsettings.json` — connection string and JWT settings.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=BuecherAusleiheDb;User Id=sa;Password=SicheresPasswort123!;Encrypt=False;"
  },
  "Jwt": {
    "Issuer": "BuecherAusleiheAPI",
    "Audience": "BuecherAusleiheClient",
    "Key": "SuperSecretKey_MinLength32Characters_Here!",
    "ExpiresIn": 15
  }
}
```

## Authentication

API uses JWT Bearer tokens. In Swagger, click **Authorize** and enter: `<your-token>`.

## Run Tests

```bash
cd Backend/Api.Tests
dotnet test
```
