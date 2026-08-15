// PURPOSE: the entry point (the "Host" project) - wires every module
// together and starts the web server. Deliberately thin: no business logic
// lives here, only setup calls into the other projects.
//
// NOTE: this now talks to the REAL idc_ety SQL Server database (see the
// "IDC_ETY" connection string in appsettings.json) - there is no local
// database file to create/seed anymore, so this file no longer touches
// EnsureCreated()/SeedData at all. GET-only, matching real DB permissions.
using Ag.Cache;
using EntityManager.Infrastructure.DependencyInjection;
using EntityManager.Presentation;
using EntityManager.Presentation.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");

builder.Services.AddAgCaching(builder.Configuration);        // connects to Redis (Ag.Cache)
builder.Services.AddSwaggerGen();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEntityManagerModule(builder.Configuration); // DB + Mediator + caching behavior + repos

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapEntityManagerEndpoints(); // registers all GET-only Agent/Office/Company routes

// Sandbox-only write endpoint for testing (see SandboxTestEndpoints.cs) -
// never mapped at all unless UseSandboxDb is true, so it's structurally
// impossible to reach this against real production.
if (builder.Configuration.GetValue<bool>("UseSandboxDb"))
    app.MapSandboxTestEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
