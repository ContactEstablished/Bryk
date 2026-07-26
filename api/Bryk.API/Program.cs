using Bryk.Application.Common;
using Bryk.Application.Events;
using Bryk.Application.Goals;
using Bryk.Application.Onboarding;
using Bryk.Application.Profile;
using Bryk.Application.Training;
using Bryk.Application.Validators;
using Bryk.Application.Zones;
using Bryk.Domain.Interfaces;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Interceptors;
using Bryk.Infrastructure.Repositories;
using Bryk.Infrastructure.Services;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Replace built-in model validation with FluentValidation
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.SuppressModelStateInvalidFilter = true);

builder.Services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>();
builder.Services.AddEndpointsApiExplorer();

// API versioning — URL-segment primary, header secondary, strict mode
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("api-version"));
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// TODO: When a v2 API ships, iterate over IApiVersionDescriptionProvider.ApiVersionDescriptions
// to register a SwaggerDoc per discovered version instead of hardcoding "v1".
// Configure Swagger/OpenAPI (Swashbuckle generates the spec)
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Bryk API",
        Version = "v1",
        Description = "TSS Training Planner API for endurance athletes"
    });
});

// Database configuration - ADD NULL CHECK
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

// Database configuration
builder.Services.AddSingleton<AuditableEntityInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null));
    options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
});

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Vue dev servers
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Repositories
builder.Services.AddScoped<IAthleteRepository, AthleteRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<ITrainingPlanRepository, TrainingPlanRepository>();
builder.Services.AddScoped<IAthleteSportZoneRepository, AthleteSportZoneRepository>();
builder.Services.AddScoped<IWorkoutRepository, WorkoutRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<ITrainingPlanService, TrainingPlanService>();
builder.Services.AddScoped<IStructuredWorkoutService, StructuredWorkoutService>();
builder.Services.AddScoped<IThisWeekService, ThisWeekService>();
builder.Services.AddScoped<IZoneService, ZoneService>();
builder.Services.AddScoped<Bryk.Application.Training.Load.ILoadService, Bryk.Application.Training.Load.LoadService>();
builder.Services.AddScoped<Bryk.Application.Training.Workouts.IWorkoutService, Bryk.Application.Training.Workouts.WorkoutService>();
builder.Services.AddScoped<Bryk.Application.Analytics.IAnalyticsService, Bryk.Application.Analytics.AnalyticsService>();
builder.Services.AddScoped<Bryk.Application.Calendar.ICalendarService, Bryk.Application.Calendar.CalendarService>();
builder.Services.AddScoped<Bryk.Application.Training.Periodization.IPeriodizationService, Bryk.Application.Training.Periodization.PeriodizationService>();

var app = builder.Build();

// Global exception handling — must be first middleware
app.UseMiddleware<Bryk.API.Middleware.ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Enable Swagger (generates OpenAPI spec)
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "openapi/{documentName}.json";
    });
    
    // Use Scalar instead of SwaggerUI for beautiful documentation
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Bryk API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowVueFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the implicit Program type generated by top-level statements so that
// WebApplicationFactory<Program> in Bryk.API.Tests can discover the entry point.
public partial class Program { }
