using Dapper;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

DefaultTypeMap.MatchNamesWithUnderscores = true;

var connectionString =
    builder.Configuration["DB_CONNECTION_STRING"]
    ?? "Host=localhost;Port=5432;Database=smarthome;Username=postgres;Password=postgres";

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();


app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }));


// --------------------------------------------------
// GET DEVICES
// --------------------------------------------------

app.MapGet("/api/v1/devices", async () =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    const string sql = """
        SELECT
            d.id,
            d.house_id,
            d.type_id AS device_type_id,
            d.serial_number,
            d.name,
            d.location,
            d.status,
            d.created_at,

            dt.name AS device_type_name,
            dt.unit

        FROM devices d

        JOIN device_types dt
            ON dt.id = d.type_id

        ORDER BY d.id
        """;

    var devices =
        await connection.QueryAsync<DeviceResponse>(sql);

    return Results.Ok(devices);
});


// --------------------------------------------------
// GET DEVICE
// --------------------------------------------------

app.MapGet("/api/v1/devices/{id:int}", async (int id) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    const string sql = """
        SELECT
            d.id,
            d.house_id,
            d.type_id AS device_type_id,
            d.serial_number,
            d.name,
            d.location,
            d.status,
            d.created_at,

            dt.name AS device_type_name,
            dt.unit

        FROM devices d

        JOIN device_types dt
            ON dt.id = d.type_id

        WHERE d.id = @Id
        """;

    var device =
        await connection.QuerySingleOrDefaultAsync<DeviceResponse>(
            sql,
            new { Id = id });

    if (device == null)
    {
        return Results.NotFound(
            new { error = "Device not found" });
    }

    return Results.Ok(device);
});


// --------------------------------------------------
// CREATE DEVICE
// --------------------------------------------------

app.MapPost(
    "/api/v1/devices",
    async (CreateDeviceRequest request) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    // Проверяем House

    var houseExists =
        await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM houses
                WHERE id = @HouseId
            )
            """,
            new { request.HouseId });

    if (!houseExists)
    {
        return Results.NotFound(
            new { error = "House not found" });
    }


    // Проверяем DeviceType

    var typeExists =
        await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM device_types
                WHERE id = @DeviceTypeId
            )
            """,
            new { request.DeviceTypeId });

    if (!typeExists)
    {
        return Results.NotFound(
            new { error = "Device type not found" });
    }


    // Проверяем serial number

    var serialExists =
        await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM devices
                WHERE serial_number = @SerialNumber
            )
            """,
            new { request.SerialNumber });

    if (serialExists)
    {
        return Results.Conflict(
            new
            {
                error =
                    "Device with this serial number already exists"
            });
    }


    const string sql = """
        INSERT INTO devices
        (
            house_id,
            type_id,
            serial_number,
            name,
            location,
            status
        )
        VALUES
        (
            @HouseId,
            @DeviceTypeId,
            @SerialNumber,
            @Name,
            @Location,
            'inactive'
        )

        RETURNING id
        """;

    var id =
        await connection.ExecuteScalarAsync<int>(
            sql,
            request);

    return Results.Created(
        $"/api/v1/devices/{id}",
        new
        {
            id,
            request.HouseId,
            request.DeviceTypeId,
            request.SerialNumber,
            request.Name,
            request.Location,
            status = "inactive"
        });
});


// --------------------------------------------------
// UPDATE DEVICE
// --------------------------------------------------

app.MapPut(
    "/api/v1/devices/{id:int}",
    async (
        int id,
        UpdateDeviceRequest request) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    const string sql = """
        UPDATE devices

        SET
            name = COALESCE(@Name, name),
            location = COALESCE(@Location, location),
            status = COALESCE(@Status, status)

        WHERE id = @Id

        RETURNING
            id,
            house_id,
            type_id AS device_type_id,
            serial_number,
            name,
            location,
            status,
            created_at
        """;

    var device =
        await connection.QuerySingleOrDefaultAsync<DeviceResponse>(
            sql,
            new
            {
                Id = id,
                request.Name,
                request.Location,
                request.Status
            });

    if (device == null)
    {
        return Results.NotFound(
            new { error = "Device not found" });
    }

    return Results.Ok(device);
});


// --------------------------------------------------
// DELETE DEVICE
// --------------------------------------------------

app.MapDelete(
    "/api/v1/devices/{id:int}",
    async (int id) =>
{
    await using var connection =
        new NpgsqlConnection(connectionString);

    try
    {
        var rows =
            await connection.ExecuteAsync(
                """
                DELETE FROM devices
                WHERE id = @Id
                """,
                new { Id = id });

        if (rows == 0)
        {
            return Results.NotFound(
                new { error = "Device not found" });
        }

        return Results.NoContent();
    }
    catch (PostgresException ex)
        when (ex.SqlState == "23503")
    {
        /*
         * Device имеет TelemetryData.
         * FK запрещает удаление.
         */

        return Results.Conflict(
            new
            {
                error =
                    "Device has telemetry data and cannot be deleted"
            });
    }
});


app.Run();


// --------------------------------------------------
// Contracts
// --------------------------------------------------

record CreateDeviceRequest(
    int HouseId,
    int DeviceTypeId,
    string SerialNumber,
    string Name,
    string Location);


record UpdateDeviceRequest(
    string? Name,
    string? Location,
    string? Status);


record DeviceResponse(
    int Id,
    int HouseId,
    int DeviceTypeId,
    string SerialNumber,
    string Name,
    string Location,
    string Status,
    DateTime CreatedAt,
    string? DeviceTypeName = null,
    string? Unit = null);