# Bryk - TSS Training Planner Backend

## Project Structure

```
Bryk/
├── Bryk.Domain/              # Core business entities and domain logic
│   └── Entities/
│       ├── Mesocycle.cs     # 12-week training block
│       ├── Week.cs          # Weekly container
│       ├── Day.cs           # Daily training plan
│       ├── Exercise.cs      # Exercise library
│       └── DayExercise.cs   # Workouts assigned to days with metrics
│
├── Bryk.Application/         # Business logic, DTOs, interfaces
│   ├── DTOs/
│   │   ├── Mesocycle/      # Mesocycle DTOs
│   │   ├── Week/           # Week DTOs
│   │   ├── Day/            # Day DTOs
│   │   └── Exercise/       # Exercise DTOs
│   └── Interfaces/
│       ├── IMesocycleService.cs
│       ├── IWeekService.cs
│       ├── IDayService.cs
│       └── IExerciseService.cs
│
├── Bryk.Infrastructure/      # Data access, external services
│   └── Data/
│       └── ApplicationDbContext.cs # EF Core DbContext
│
└── Bryk.API/                 # Web API controllers, startup
    └── (to be created)
```

## Domain Model

### Mesocycle
- Top-level training block (e.g., 12-week base build)
- Contains configuration for TSS progression
- Generates weeks based on build:recovery ratio

### Week
- 7-day container within a mesocycle
- Has target TSS and recovery/build status
- Contains breakdown by sport and intensity

### Day
- Individual training day
- Has target TSS and optional rest day flag
- Contains collection of assigned exercises

### Exercise
- Workout library (swim/bike/run/strength)
- Reusable across multiple days
- Contains base TSS value and description

### DayExercise
- Junction entity linking exercises to specific days
- Stores actual performance metrics:
  - HR data, calories, pace metrics
  - Weather conditions
  - Performance comparisons
  - Zone distribution

## Technology Stack
- .NET 10.0
- Entity Framework Core 10.0
- SQL Server
- Clean Architecture pattern

## Next Steps

1. **Implement Service Layer**
   - Create MesocycleService
   - Create WeekService
   - Create DayService
   - Create ExerciseService

2. **Create API Controllers**
   - MesocyclesController
   - WeeksController
   - DaysController
   - ExercisesController

3. **Database Setup**
   - Create initial migration
   - Seed sample data

4. **Add Business Logic**
   - TSS calculation algorithms
   - Week generation logic
   - Performance comparison logic

## Running the Project

```bash
# Restore packages
dotnet restore

# Create database migration
dotnet ef migrations add InitialCreate --project Bryk.Infrastructure --startup-project Bryk.API

# Update database
dotnet ef database update --project Bryk.Infrastructure --startup-project Bryk.API

# Run the API
dotnet run --project Bryk.API
```

## Database Connection

Update `appsettings.json` in Bryk.API:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=BrykDb;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

## Project Features

✅ **Complete Clean Architecture**
- Domain Layer with 5 entities
- Application Layer with DTOs and service interfaces
- Infrastructure Layer with EF Core
- API Layer ready for controllers

✅ **Advanced Metrics**
- Heart rate zone tracking
- Pace/power metrics
- Performance comparisons
- Weather tracking

✅ **Smart Design**
- Automatic audit fields (CreatedAt/UpdatedAt)
- Proper cascade delete rules
- Guid primary keys
- Nullable reference types enabled
