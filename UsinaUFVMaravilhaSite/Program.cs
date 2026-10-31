using System.Text.Json.Serialization;
using UsinaUFVMaravilhaSite.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<PlantOptions>(builder.Configuration.GetSection(PlantOptions.SectionName));
builder.Services.AddSingleton<PlantStateStore>();

var simulate = builder.Configuration.GetSection(PlantOptions.SectionName).GetValue<bool>(nameof(PlantOptions.Simulate));
if (simulate)
{
    builder.Services.AddSingleton<IPlantDataSource, SimulatedPlantDataSource>();
}
else
{
    builder.Services.AddSingleton<IPlantDataSource, ModbusPlantDataSource>();
}

builder.Services.AddHostedService<PlantPollingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/api/plant/state", (PlantStateStore store) => Results.Ok(store.Current));

app.MapPost("/api/plant/reset/{target}", async (string target, IPlantDataSource dataSource, CancellationToken ct) =>
{
    if (!Enum.TryParse<ResetTarget>(target, ignoreCase: true, out var resetTarget))
    {
        return Results.BadRequest(new { error = $"Alvo de reset inválido: {target}" });
    }

    try
    {
        await dataSource.ResetAsync(resetTarget, ct);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/plant/breaker/{command}", async (string command, IPlantDataSource dataSource, CancellationToken ct) =>
{
    if (!Enum.TryParse<BreakerCommand>(command, ignoreCase: true, out var breakerCommand))
    {
        return Results.BadRequest(new { error = $"Comando de disjuntor inválido: {command}" });
    }

    try
    {
        await dataSource.BreakerCommandAsync(breakerCommand, ct);
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();
