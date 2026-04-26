# Bryk

**TSS-based training planner for endurance athletes.**

Bryk helps cyclists, runners, triathletes, and multisport athletes build structured training plans using Training Stress Score (TSS) as the primary load metric. It generates progressive mesocycle plans, tracks daily workout execution, and provides load analytics across the training block.

---

## What It Does

- **Mesocycle planning** — define a training block by duration, weekly TSS targets, progression rate, build/recovery ratio, and weekly pattern type
- **Week and day scaffolding** — automatically generates weeks and days within a mesocycle based on TSS progression rules
- **Workout tracking** — log completed workouts with TSS, heart rate zones, pace metrics, weather, and performance comparison data
- **Exercise library** — maintain a reusable library of workouts across sport types (bike, run, swim, strength)
- **TSS intelligence** — smart TSS calculation engine that estimates load from workout inputs

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend API | .NET 10 — controller-based Web API |
| Frontend | Vue 3 + Composition API + TypeScript |
| Primary ORM | Entity Framework Core (code-first) |
| Secondary ORM | Dapper (complex/performance-sensitive queries) |
| Database | SQL Server (default) / SQLite (local/Electron mode) |
| API Docs | Scalar (OpenAPI) |
| Validation | FluentValidation |
| Desktop | Electron.js (optional) |

---

## Architecture

Bryk follows Clean Architecture with four layers:

```
Bryk.Domain          — entities, no external dependencies
Bryk.Application     — interfaces, DTOs, validators
Bryk.Infrastructure  — EF Core DbContext, migrations, service implementations
Bryk.API             — controllers, middleware, Program.cs
```

**Key conventions:**
- Services consume `ApplicationDbContext` directly — no repository wrapper
- Controllers are thin — all logic lives in services
- No entities cross the API boundary — DTOs only
- Global exception middleware handles all unhandled exceptions
- FluentValidation handles all input validation
- Dapper is the designated escape valve for complex queries

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (or SQL Server Express / LocalDB for local dev)
- Node.js 20+ (for the Vue frontend)

### Backend Setup

```bash
# Clone the repo
git clone https://github.com/your-org/bryk.git
cd bryk/api

# Restore packages
dotnet restore

# Set your connection string in appsettings.Development.json
# "ConnectionStrings": { "DefaultConnection": "Server=...;Database=Bryk;..." }

# Apply migrations
dotnet ef database update --project Bryk.Infrastructure --startup-project Bryk.API

# Run the API
dotnet run --project Bryk.API
```

API will be available at `https://localhost:5001`
Scalar API docs at `https://localhost:5001/scalar`

### Frontend Setup

```bash
cd bryk/ui

# Install dependencies
npm install

# Run dev server
npm run dev
```

Frontend will be available at `http://localhost:5173`

---

## Project Structure

```
bryk/
├── api/
│   ├── Bryk.Domain/
│   │   └── Entities/          # Mesocycle, Week, Day, Exercise, DayExercise
│   ├── Bryk.Application/
│   │   ├── DTOs/              # Request/response shapes per domain
│   │   ├── Interfaces/        # Service contracts
│   │   └── Validators/        # FluentValidation validators
│   ├── Bryk.Infrastructure/
│   │   ├── Data/              # ApplicationDbContext, migrations
│   │   └── Services/          # Service implementations
│   └── Bryk.API/
│       ├── Controllers/       # Thin REST controllers
│       ├── Middleware/        # Global exception handler
│       └── Program.cs
└── ui/
    ├── src/
    │   ├── components/        # Reusable Vue components
    │   ├── composables/       # Shared composition functions
    │   ├── router/            # Vue Router 4
    │   ├── services/          # API service layer
    │   ├── stores/            # Pinia stores
    │   └── views/             # Route-level page components
    └── package.json
```

---

## Data Model

```
Mesocycle
  └── Week (1:many, cascade delete)
       └── Day (1:many, cascade delete)
            └── DayExercise (1:many, cascade delete)
                     └── Exercise (many:1, restrict delete)
```

All entities use `Guid` primary keys and carry `CreatedAt` / `UpdatedAt` audit timestamps managed automatically by the DbContext.

---

## API Overview

All endpoints are versioned under `/api/v1/`.

| Resource | Endpoints |
|---|---|
| Mesocycle | GET, GET by id, GET with weeks, POST, PUT, DELETE |
| Week | GET by id, GET with days, PUT, POST copy |
| Day | GET by id, GET with exercises, PUT, POST add exercise, PUT update exercise, DELETE remove exercise |
| Exercise | GET all (filterable), GET by id, POST, PUT, DELETE, POST duplicate |

Full interactive documentation is available via Scalar at `/scalar` in development mode.

---

## Configuration

All configuration lives in `appsettings.json` / `appsettings.Development.json`. No hardcoded values anywhere in the codebase.

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `DatabaseProvider` | `SqlServer` \| `MySql` \| `Sqlite` (Electron mode) |
| `Electron:Enabled` | `true` \| `false` |
| `AI:Provider` | Active AI provider key |
| `AI:Providers:{name}:ApiKey` | Per-provider API key |

---

## Electron / Local Desktop Mode

Bryk can run as a standalone desktop application via Electron.js — no web server setup required. In Electron mode, the user selects their preferred local database (SQL Server, MySQL, or SQLite) on first launch via a setup wizard. Configuration is persisted locally and the API starts embedded alongside the Electron shell.

---

## AI Integration

Bryk supports connecting to external AI providers to enhance training plan recommendations and workout analysis. Supported providers:

- Anthropic Claude
- OpenAI ChatGPT
- DeepSeek
- Google Gemini

API keys are stored locally and never transmitted to Bryk servers.

---

## Development Conventions

- `.NET`: primary constructor syntax, no hardcoded IDs, EF Core code-first migrations, Dapper only when justified
- `Vue`: `<script setup lang="ts">` only, Pinia for state, all API calls through `src/services/`, typed props and emits
- `Git`: commit after each accepted task, one feature per commit, meaningful commit messages

---

## License

MIT
