var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var random = new Random();

app.MapGet("/temperature", (string location = "unknown") =>
{
    var temperature = Math.Round(
        random.NextDouble() * 40 - 10,
        1);

    return Results.Ok(new
    {
       location,
       temperature,
       timestamp = DateTime.UtcNow 
    });

});

app.Run();
