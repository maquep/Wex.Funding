using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Wex.Funding.Api.Endpoints;
using Wex.Funding.Api.Middleware;
using Wex.Funding.Api.Validators;
using Wex.Funding.Application.Cards;
using Wex.Funding.Application.Transactions;
using Wex.Funding.Infrastructure;
using Wex.Funding.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) =>
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithCorrelationId()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Wex Funding API", Version = "v1" });
});

builder.Services.AddInfrastructure(builder.Configuration);

// Use cases — registered as transient because they have no state
builder.Services.AddTransient<CreateCard>();
builder.Services.AddTransient<RecordTransaction>();
builder.Services.AddTransient<GetTransactionInCurrency>();
builder.Services.AddTransient<GetCardBalanceInCurrency>();

// Validators
builder.Services.AddScoped<IValidator<CreateCardRequest>, CreateCardRequestValidator>();
builder.Services.AddScoped<IValidator<RecordTransactionRequest>, RecordTransactionRequestValidator>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres") ?? string.Empty);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WexDbContext>();
    await db.Database.MigrateAsync();
}

app.MapCardEndpoints();
app.MapTransactionEndpoints();
app.MapHealthChecks("/health");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
