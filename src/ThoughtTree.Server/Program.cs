// Composition root of the sync server (ADR-040). Endpoints, hub and stores come with the first server release (ADR-074).
var app = WebApplication.CreateBuilder(args).Build();
app.Run();
