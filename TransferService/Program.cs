using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TransferService.Data;
using TransferService.Middleware;
using TransferService.Services;
using TransferService.Messaging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq:5341"));

var connectionString =
    builder.Configuration.GetConnectionString("TransferDb")
    ?? throw new InvalidOperationException("Transfer database connection string is missing.");

builder.Services.AddDbContext<TransferDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<TransferWorkflowConsumer>();
    configurator.AddEntityFrameworkOutbox<TransferDbContext>(outbox =>
    {
        outbox.UseMySql();
        outbox.UseBusOutbox();
    });

    configurator.UsingRabbitMq((context, bus) =>
    {
        bus.Host(
            builder.Configuration["RabbitMQ:HostName"] ?? "rabbitmq",
            ushort.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
            "/",
            host =>
            {
                host.Username(builder.Configuration["RabbitMQ:UserName"] ?? "guest");
                host.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        bus.ReceiveEndpoint("transfer-workflow", endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Exponential(5, TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            endpoint.UseDelayedRedelivery(retry => retry.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
            endpoint.ConfigureConsumer<TransferWorkflowConsumer>(context);
        });
    });
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer Missing");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience Missing");
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret Missing");

var secretBytes = Encoding.UTF8.GetBytes(jwtSecret);
if (secretBytes.Length < 32)
{
    throw new InvalidOperationException("JWT Secret must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddScoped<ITransferService, TransferService.Services.TransferService>();
builder.Services.AddHttpClient("wallet", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["WalletService:BaseUrl"]
        ?? "http://wallet-service:8040/");
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
