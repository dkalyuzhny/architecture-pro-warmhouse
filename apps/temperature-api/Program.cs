var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

object CreateTemperatureResponse(string location, string sensorId)
{
    var value = Math.Round(
        Random.Shared.NextDouble() * 40 - 10,
        1);

    return new
    {
        value,
        unit = "°C",
        timestamp = DateTime.UtcNow,
        location,
        status = "online",
        sensor_id = sensorId,
        sensor_type = "temperature",
        description = "Temperature sensor"
    };
}



// GET /temperature?location=living-room
app.MapGet("/temperature", (string location = "unknown") =>
{
    return Results.Ok(
        CreateTemperatureResponse(location, "unknown")
    );
});



// GET /temperature/1
app.MapGet("/temperature/{sensorId}", (string sensorId) =>
{
    return Results.Ok(
        CreateTemperatureResponse(
            $"sensor-{sensorId}",
            sensorId
        )
    );
});

app.Run();
