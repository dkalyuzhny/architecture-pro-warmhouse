using Dapper;
using Npgsql;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

DefaultTypeMap.MatchNamesWithUnderscores = true;

var connectionString =
    builder.Configuration["DB_CONNECTION_STRING"]
    ?? "Host=localhost;Port=5432;Database=smarthome;Username=postgres;Password=postgres";


var temperatureApiUrl =
    builder.Configuration["TEMPERATURE_API_URL"]
    ?? "http://localhost:8081";


builder.Services.AddHttpClient(
    "temperature-api",
    client =>
    {
        client.BaseAddress =
            new Uri(temperatureApiUrl);

        client.Timeout =
            TimeSpan.FromSeconds(5);
    });


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();


app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }));


// --------------------------------------------------
// CREATE TELEMETRY
// --------------------------------------------------

app.MapPost(
    "/api/v1/telemetry",
    async (CreateTelemetryRequest request) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    // Проверяем Device

    var deviceExists =
        await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM devices
                WHERE id = @DeviceId
            )
            """,
            new { request.DeviceId });

    if (!deviceExists)
    {
        return Results.NotFound(
            new { error = "Device not found" });
    }


    const string sql = """
        INSERT INTO telemetry_data
        (
            device_id,
            value,
            status,
            timestamp
        )
        VALUES
        (
            @DeviceId,
            @Value,
            @Status,
            @Timestamp
        )

        RETURNING
            id,
            device_id,
            value,
            status,
            timestamp
        """;


    var timestamp =
        request.Timestamp ?? DateTime.UtcNow;


    var telemetry =
        await connection.QuerySingleAsync<TelemetryData>(
            sql,
            new
            {
                request.DeviceId,
                request.Value,
                Status =
                    request.Status ?? "active",
                Timestamp = timestamp
            });


    return Results.Created(
        $"/api/v1/telemetry/{request.DeviceId}",
        telemetry);
});


// --------------------------------------------------
// GET TELEMETRY
// --------------------------------------------------

app.MapGet(
    "/api/v1/telemetry/{deviceId:int}",
    async (
        int deviceId,
        bool latest = false,
        int limit = 100) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);


    var deviceExists =
        await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM devices
                WHERE id = @DeviceId
            )
            """,
            new { DeviceId = deviceId });


    if (!deviceExists)
    {
        return Results.NotFound(
            new { error = "Device not found" });
    }


    // Только последнее значение

    if (latest)
    {
        const string latestSql = """
            SELECT
                id,
                device_id,
                value,
                status,
                timestamp

            FROM telemetry_data

            WHERE device_id = @DeviceId

            ORDER BY
                timestamp DESC,
                id DESC

            LIMIT 1
            """;

        var telemetry =
            await connection
                .QuerySingleOrDefaultAsync<TelemetryData>(
                    latestSql,
                    new { DeviceId = deviceId });

        if (telemetry == null)
        {
            return Results.NotFound(
                new
                {
                    error =
                        "Telemetry not found"
                });
        }

        return Results.Ok(telemetry);
    }


    // История значений

    limit = Math.Clamp(limit, 1, 1000);

    const string historySql = """
        SELECT
            id,
            device_id,
            value,
            status,
            timestamp

        FROM telemetry_data

        WHERE device_id = @DeviceId

        ORDER BY
            timestamp DESC,
            id DESC

        LIMIT @Limit
        """;


    var items =
        await connection.QueryAsync<TelemetryData>(
            historySql,
            new
            {
                DeviceId = deviceId,
                Limit = limit
            });


    return Results.Ok(
        new
        {
            deviceId,
            items
        });
});


// --------------------------------------------------
// COMPATIBILITY WITH EXISTING TEMPERATURE API
// --------------------------------------------------

app.MapGet(
    "/api/v1/telemetry/temperature/{location}",
    async (
        string location,
        IHttpClientFactory factory) =>
{
    var client =
        factory.CreateClient(
            "temperature-api");

    try
    {
        var result =
            await client.GetFromJsonAsync<TemperatureResponse>(
                $"/temperature?location={Uri.EscapeDataString(location)}");

        if (result == null)
        {
            return Results.Problem(
                "Temperature API returned empty response");
        }

        return Results.Ok(result);
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(
            StatusCodes.Status502BadGateway);
    }
});


app.Run();


// --------------------------------------------------
// Contracts
// --------------------------------------------------

record CreateTelemetryRequest(
    int DeviceId,
    double Value,
    string? Status,
    DateTime? Timestamp);


record TelemetryData(
    long Id,
    int DeviceId,
    double Value,
    string Status,
    DateTime Timestamp);


record TemperatureResponse(
    double Value,
    string? Unit,
    string? Location,
    string? Status,
    DateTime? Timestamp);