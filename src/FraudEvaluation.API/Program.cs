using System.Reflection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
// Register MediatR handlers from the Application project
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(FraudEvaluation.Application.MediatRMarker).Assembly));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Expose OpenAPI and Swagger UI in Development
    app.MapOpenApi();

    // Scalar.AspNetCore integration (development only, no authentication)
    try
    {
        var scalarAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Scalar.AspNetCore", StringComparison.OrdinalIgnoreCase));
        if (scalarAssembly == null)
        {
            scalarAssembly = Assembly.Load("Scalar.AspNetCore");
        }

        if (scalarAssembly != null)
        {
            var extensionMethods = scalarAssembly.GetTypes()
                .Where(t => t.IsSealed && t.IsAbstract)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.Name.Contains("UseScalar", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("MapScalar", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("UseScalarUI", StringComparison.OrdinalIgnoreCase)
                         || m.Name.Contains("MapScalarUI", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var method in extensionMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(app.GetType()))
                {
                    method.Invoke(null, new object[] { app });
                    break;
                }

                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(Microsoft.AspNetCore.Builder.IApplicationBuilder)))
                {
                    method.Invoke(null, new object[] { app });
                    break;
                }
            }
        }
    }
    catch
    {
        // Scalar integration is optional in development; ignore failures
    }
}

app.UseHttpsRedirection();
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// POST endpoint to receive TaxId (numbers) and Amount (decimal)
app.MapPost("/transactions", async (TransactionRequest req, IMediator mediator) =>
{
    if (string.IsNullOrWhiteSpace(req.TaxId) || !req.TaxId.All(char.IsDigit))
    {
        return Results.BadRequest(new { error = "TaxId must contain only digits." });
    }

    if (req.Amount <= 0)
    {
        return Results.BadRequest(new { error = "Amount must be greater than zero." });
    }

    // Delegate creation to MediatR handler in the Application layer
    var command = new FraudEvaluation.Application.Commands.CreateTransactionCommand(req.TaxId, req.Amount);
    var result = await mediator.Send(command);

    return Results.Created($"/transactions/{result.Id}", result);
})
.WithName("CreateTransaction");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record TransactionRequest(string TaxId, decimal Amount);
