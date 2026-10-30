using CPS.Components;
using MudBlazor.Services;
using ApexCharts;
using System.Net.Http.Json;
using Microsoft.AspNetCore.HttpLogging;
using System.Text.Json;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpClient("LocalApi", client =>
{
    client.BaseAddress = new Uri(
        "https://localhost:7242"
    );
});
builder.Services.AddApexCharts();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/sensor", (CpsSensorRequest data) =>
{
    Console.WriteLine("HTTP POST RECEIVED!");
    Console.WriteLine($"Temperature: {data.Temperature} °C");
    Console.WriteLine($"Humidity: {data.Humidity:F1} %");

    return Results.Ok(new
    {
        message = "CPS sensor data received successfully!",
        rpm = data.Rpm,
        torque = data.Torque,
        temperature = data.Temperature,
        vacuum = data.Vacuum,
        humidity = data.Humidity,
        receivedAt = DateTime.Now
    });
});


app.MapPost("/api/odoo-test", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/mrp.production/search_read");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add("X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            domain = Array.Empty<object>(),
            fields = new[] { "id", "name", "state" },
            limit = 1
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                status = (int)response.StatusCode,
                message = "Odoo request failed. Check API access."
            });
        }

        var json = await response.Content.ReadAsStringAsync();

        return Results.Ok(new
        {
            success = true,
            message = "Odoo connection successful!",
            data = System.Text.Json.JsonDocument.Parse(json).RootElement
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Could not connect to Odoo.");
    }
});


app.MapPost("/api/odoo-fields", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/quality.check/fields_get");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add("X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            attributes = new[] { "string", "type", "required" }
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                status = (int)response.StatusCode,
                message = "Unable to read Odoo fields."
            }, statusCode: (int)response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync();

        using var document =
            System.Text.Json.JsonDocument.Parse(json);

        var fields = document.RootElement
            .EnumerateObject()
            .Select(f => new
            {
                name = f.Name,
                label = f.Value.TryGetProperty("string", out var label)
                    ? label.GetString() : "",
                type = f.Value.TryGetProperty("type", out var type)
                    ? type.GetString() : ""
            })
            .ToList();

        return Results.Ok(new
        {
            success = true,
            model = "quality.check",
            totalFields = fields.Count,
            fields
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Could not inspect Odoo fields.");
    }
});


app.MapPost("/api/odoo-mixer-info", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem("Odoo config missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        async Task<string> ReadOdoo(
            string model,
            string[] fields)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl.TrimEnd('/')}/json/2/{model}/search_read");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", apiKey);

            if (!string.IsNullOrWhiteSpace(database))
                request.Headers.Add("X-Odoo-Database", database);

            request.Content = JsonContent.Create(new
            {
                domain = Array.Empty<object>(),
                fields,
                limit = 20
            });

            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return $"HTTP {(int)response.StatusCode}";

            return content;
        }

        var workcenters = await ReadOdoo(
            "mrp.workcenter",
            new[] { "id", "name" });

        var workorders = await ReadOdoo(
            "mrp.workorder",
            new[] { "id", "name", "state", "workcenter_id" });

        return Results.Ok(new
        {
            success = true,
            workcenters,
            workorders
        });
    }
    catch (Exception)
    {
        return Results.Problem("Odoo request failed.");
    }
});


app.MapPost("/api/odoo-productivity-fields", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem("Odoo configuration missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/mrp.workcenter.productivity/fields_get");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add("X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            attributes = new[]
            {
                "string",
                "type",
                "required",
                "readonly",
                "relation"
            }
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                status = (int)response.StatusCode,
                message = "Could not inspect productivity fields."
            }, statusCode: (int)response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync();

        using var document =
            System.Text.Json.JsonDocument.Parse(json);

        var fields = document.RootElement
            .EnumerateObject()
            .Select(f => new
            {
                name = f.Name,
                label = f.Value.TryGetProperty("string", out var label)
                    ? label.GetString() : "",
                type = f.Value.TryGetProperty("type", out var type)
                    ? type.GetString() : "",
                required = f.Value.TryGetProperty("required", out var req)
                    && req.GetBoolean(),
                readOnly = f.Value.TryGetProperty("readonly", out var ro)
                    && ro.GetBoolean(),
                relation = f.Value.TryGetProperty("relation", out var rel)
                    ? rel.GetString() : null
            })
            .ToList();

        return Results.Ok(new
        {
            success = true,
            model = "mrp.workcenter.productivity",
            totalFields = fields.Count,
            fields
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Could not connect to Odoo.");
    }
});


app.MapPost("/api/odoo-oee-info", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        async Task<object> ReadOdoo(
            string model,
            string[] fields,
            object[] domain)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl.TrimEnd('/')}/json/2/{model}/search_read");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", apiKey);

            if (!string.IsNullOrWhiteSpace(database))
            {
                request.Headers.Add(
                    "X-Odoo-Database", database);
            }

            request.Content = JsonContent.Create(new
            {
                domain,
                fields,
                limit = 30
            });

            using var response =
                await client.SendAsync(request);

            var json =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new
                {
                    success = false,
                    status = (int)response.StatusCode,
                    model
                };
            }

            using var document =
                System.Text.Json.JsonDocument.Parse(json);

            return new
            {
                success = true,
                data = document.RootElement.Clone()
            };
        }

        var workcenter = await ReadOdoo(
            "mrp.workcenter",
            new[] { "id", "name", "company_id" },
            new object[]
            {
                new object[] { "id", "=", 3 }
            });

        var lossReasons = await ReadOdoo(
            "mrp.workcenter.productivity.loss",
            new[] { "id", "name", "loss_type" },
            Array.Empty<object>());

        return Results.Ok(new
        {
            workcenter,
            lossReasons
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "Failed to retrieve OEE configuration.");
    }
});


app.MapPost("/api/cps-odoo-preview", async (
    CpsSensorRequest data,
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo API configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/mrp.workcenter/search_read");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add(
                "X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            domain = new object[]
            {
                new object[] { "id", "=", 3 }
            },
            fields = new[] { "id", "name", "company_id" },
            limit = 1
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                message = "Odoo connection failed.",
                odooStatus = (int)response.StatusCode
            }, statusCode: 502);
        }

        using var document =
            System.Text.Json.JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

        if (document.RootElement.ValueKind !=
                System.Text.Json.JsonValueKind.Array ||
            document.RootElement.GetArrayLength() == 0)
        {
            return Results.NotFound(new
            {
                success = false,
                message = "Mixing work center not found."
            });
        }

        return Results.Ok(new
        {
            success = true,
            mode = "PREVIEW ONLY",
            message = "CPS data received; Odoo Mixing work center verified. No sensor data was saved to Odoo.",
            workcenterId = 3,
            cpsData = new
            {
                data.Rpm,
                data.Torque,
                data.Temperature,
                data.Vacuum,
                data.Humidity
            },
            timestamp = DateTimeOffset.Now
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "CPS to Odoo preview request failed.");
    }
});

app.MapPost("/api/cps-odoo-save", async (
    CpsSensorRequest data,
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/x_cps_sensor_log/create");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add(
                "X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            vals_list = new[]
            {
                new
                {
                    x_name = $"CPS Reading {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    x_studio_mixer_rpm = data.Rpm,
                    x_studio_torque = data.Torque,
                    x_studio_temperature = data.Temperature,
                    x_studio_vacuum = data.Vacuum,
                    x_studio_humidity = data.Humidity
                }
            }
        });

        using var response = await client.SendAsync(request);

        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {

                success = false,
                message = "Odoo save failed.",
                odooStatus = (int)response.StatusCode
            }, statusCode: 502);
        }

        return Results.Ok(new
        {

            success = true,
            message = "CPS data saved to Odoo!",
            odooResponse = result
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "CPS Odoo save request failed.");
    }
});

// SPC - READ CPS SENSOR HISTORY

app.MapGet("/api/spc-history", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/x_cps_sensor_log/search_read");

        request.Headers.Authorization =
           new System.Net.Http.Headers.AuthenticationHeaderValue(
               "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add(
                "X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            domain = Array.Empty<object>(),

            fields = new[]
            {
                "id",
                "create_date",
                "x_studio_mixer_rpm",
                "x_studio_torque",
                "x_studio_temperature",
                "x_studio_vacuum",
                "x_studio_humidity"

            },

            order = "create_date desc, id desc",

            limit = 100
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                message = "Failed to read CPS sensor history.",
                odooStatus = (int)response.StatusCode
            }, statusCode: 502);
        }

        var result = await response.Content.ReadAsStringAsync();

        return Results.Content(
            result,
            "application/json");
    }
    catch (Exception)
    {
        return Results.Problem(
            "SPC history request failed.");
    }
});

// SPC - CALCULATE CONTROL LIMITS


app.MapGet("/api/spc-calculate", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/x_cps_sensor_log/search_read");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            request.Headers.Add(
                "X-Odoo-Database", database);
        }

        request.Content = JsonContent.Create(new
        {
            domain = Array.Empty<object>(),

            fields = new[]
            {
                "id",
                "create_date",
                "x_studio_mixer_rpm",
                "x_studio_torque",
                "x_studio_temperature",
                "x_studio_vacuum",
                "x_studio_humidity"
            },

            order = "create_date desc, id desc",
            limit = 100
        });

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Json(new
            {
                success = false,
                message = "Failed to retrieve sensor data.",
                odooStatus = (int)response.StatusCode
            }, statusCode: 502);
        }

        var json = await response.Content.ReadAsStringAsync();

        using var document =
            System.Text.Json.JsonDocument.Parse(json);

        if (document.RootElement.ValueKind !=
            System.Text.Json.JsonValueKind.Array)
        {
            return Results.Problem(
                "Unexpected Odoo response format.");
        }

        var records = document.RootElement
            .EnumerateArray()
            .ToList();

        if (records.Count < 3)
        {
            return Results.BadRequest(new
            {
                success = false,
                message = "Not enough historical readings for SPC calculation."
            });
        }

        // Odoo returns newest first.
        // Reverse to chronological order.
        records.Reverse();

        // Calculate SPC for each sensor parameter.
        object CalculateSpc(string fieldName, string parameter)
        {
            var values = records
                .Select(r => r.GetProperty(fieldName).GetDouble())
                .ToList();

            double mean = values.Average();

            var movingRanges = values
                .Skip(1)
                .Zip(
                    values,
                    (current, previous) =>
                        Math.Abs(current - previous))
                .ToList();

            double averageMR = movingRanges.Average();

            double ucl = mean + (2.66 * averageMR);
            double lcl = mean - (2.66 * averageMR);

            double actualValue = values.Last();

            string status =
                actualValue > ucl || actualValue < lcl
                    ? "Out of Control"
                    : "In Control";

            return new
            {
                parameter,
                sampleCount = values.Count,
                centerLine = Math.Round(mean, 3),
                actualValue = Math.Round(actualValue, 3),
                ucl = Math.Round(ucl, 3),
                lcl = Math.Round(lcl, 3),
                processStatus = status
            };
        }

        var results = new[]
        {
            CalculateSpc(
                "x_studio_mixer_rpm", "RPM"),

            CalculateSpc(
                "x_studio_torque", "Torque"),

            CalculateSpc(
                "x_studio_temperature", "Temperature"),

            CalculateSpc(
                "x_studio_vacuum", "Vacuum"),

            CalculateSpc(
                "x_studio_humidity", "Humidity")
        };

        return Results.Ok(new
        {
            success = true,
            mode = "SPC CALCULATION PREVIEW",
            method = "Individuals-Moving Range (I-MR)",
            message = "SPC calculated successfully. No data saved to Odoo.",
            results
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "SPC calculation failed.");
    }
});

// SPC - SAVE RESULTS TO ODOO

app.MapPost("/api/spc-save", async (
    IHttpClientFactory factory,
    IConfiguration config) =>
{
    var apiKey = config["Odoo:ApiKey"];
    var baseUrl = config["Odoo:BaseUrl"];
    var database = config["Odoo:Database"];

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(baseUrl))
    {
        return Results.Problem(
            "Odoo configuration is missing.");
    }

    try
    {
        using var client = factory.CreateClient();

        // STEP 1: Read CPS sensor history
        using var historyRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/x_cps_sensor_log/search_read");

        historyRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            historyRequest.Headers.Add(
                "X-Odoo-Database", database);
        }

        historyRequest.Content = JsonContent.Create(new
        {
            domain = Array.Empty<object>(),
            fields = new[]
            {
                "id",
                "create_date",
                "x_studio_mixer_rpm",
                "x_studio_torque",
                "x_studio_temperature",
                "x_studio_vacuum",
                "x_studio_humidity"
            },
            order = "create_date desc, id desc",
            limit = 100
        });

        using var historyResponse =
            await client.SendAsync(historyRequest);

        if (!historyResponse.IsSuccessStatusCode)
        {
            return Results.Problem(
                "Failed to retrieve CPS sensor history.");
        }

        var json =
            await historyResponse.Content.ReadAsStringAsync();

        using var document =
            System.Text.Json.JsonDocument.Parse(json);

        if (document.RootElement.ValueKind !=
            System.Text.Json.JsonValueKind.Array)
        {
            return Results.Problem(
                "Unexpected CPS history response.");
        }

        var records = document.RootElement
            .EnumerateArray()
            .ToList();

        if (records.Count < 3)
        {
            return Results.BadRequest(
                "Not enough historical readings.");
        }

        records.Reverse();

        // STEP 2: Calculate SPC statistics
        object CalculateSpc(string fieldName, string parameter)
        {
            var values = records
                .Select(r => r.GetProperty(fieldName).GetDouble())
                .ToList();

            double mean = values.Average();

            var movingRanges = values
                .Skip(1)
                .Zip(
                    values,
                    (current, previous) =>
                        Math.Abs(current - previous))
                .ToList();

            double averageMR = movingRanges.Average();

            double ucl = mean + (2.66 * averageMR);
            double lcl = mean - (2.66 * averageMR);

            double actualValue = values.Last();

            string status =
                actualValue > ucl || actualValue < lcl
                    ? "Out of Control"
                    : "In Control";

            return new
            {
                x_name =
                    $"SPC {parameter} {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",

                x_studio_timestamp =
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),

                x_studio_parameter = parameter,

                x_studio_center_line = Math.Round(mean, 3),

                x_studio_actual_value = Math.Round(actualValue, 3),

                x_studio_ucl = Math.Round(ucl, 3),

                x_studio_lcl = Math.Round(lcl, 3),

                x_studio_process_status = status
            };
        }

        // STEP 3: Prepare five SPC records
        var spcRecords = new[]
        {
            CalculateSpc("x_studio_mixer_rpm", "RPM"),
            CalculateSpc("x_studio_torque", "Torque"),
            CalculateSpc("x_studio_temperature", "Temperature"),
            CalculateSpc("x_studio_vacuum", "Vacuum"),
            CalculateSpc("x_studio_humidity", "Humidity")
        };

        // STEP 4: Save results to Odoo SPC
        using var saveRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/json/2/x_spc/create");

        saveRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        if (!string.IsNullOrWhiteSpace(database))
        {
            saveRequest.Headers.Add(
                "X-Odoo-Database", database);
        }

        saveRequest.Content = JsonContent.Create(new
        {
            vals_list = spcRecords
        });

        using var saveResponse =
            await client.SendAsync(saveRequest);

        if (!saveResponse.IsSuccessStatusCode)
        {
            var odooError =
                await saveResponse.Content.ReadAsStringAsync();

            return Results.Json(new
            {
                success = false,
                message = "Failed to save SPC results.",
                odooStatus = (int)saveResponse.StatusCode
            }, statusCode: 502);
        }

        return Results.Ok(new
        {
            success = true,
            message = "SPC results saved to Odoo!",
            recordsSubmitted = spcRecords.Length
        });
    }
    catch (Exception)
    {
        return Results.Problem(
            "SPC save request failed.");
    }
});


app.Run();
public record CpsSensorRequest(
    int Rpm,
    double Torque,
    double Temperature,
    double Vacuum,
    double Humidity
    );





