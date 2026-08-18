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

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");

builder.Services.AddAgCaching(builder.Configuration);        // connects to Redis (Ag.Cache)
builder.Services.AddSwaggerGen();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddEntityManagerModule(builder.Configuration); // DB + Mediator + caching behavior + repos

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Registers all Agent/Office/Company routes. Update endpoints (see
// AgentUpdateEndpoint.cs etc.) are only mapped at all when UseSandboxDb is
// true - structurally impossible to reach against real production.
app.MapEntityManagerEndpoints(builder.Configuration.GetValue<bool>("UseSandboxDb"));

app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
