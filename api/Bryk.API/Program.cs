using Bryk.Application.Interfaces;
using Bryk.Application.Validators;
using Bryk.Infrastructure.Data;
using Bryk.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Replace built-in model validation with FluentValidation
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.SuppressModelStateInvalidFilter = true);

builder.Services.AddValidatorsFromAssemblyContaining<ValidatorPlaceholder>();
builder.Services.AddEndpointsApiExplorer();

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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

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

// Service registrations
builder.Services.AddScoped<IMesocycleService, MesocycleService>();
// builder.Services.AddScoped<IWeekService, WeekService>();
// builder.Services.AddScoped<IDayService, DayService>();
// builder.Services.AddScoped<IExerciseService, ExerciseService>();

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