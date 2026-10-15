using CPS.Components;
using MudBlazor.Services;
using ApexCharts;
using System.Net.Http.Json;

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
                data.Vacuum
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


app.Run();
public record CpsSensorRequest(
    int Rpm,
    double Torque,
    double Temperature,
    double Vacuum,
    double Humidity
    );





