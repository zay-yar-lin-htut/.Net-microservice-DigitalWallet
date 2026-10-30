using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WalletService.Data;
using WalletService.Middleware;
using WalletService.Messaging;
using WalletService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq:5341"));

var connectionString =
    builder.Configuration.GetConnectionString("WalletDb")
    ?? throw new InvalidOperationException("Wallet database connection string is missing.");

builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<ProcessWalletOperationConsumer>();
    configurator.AddEntityFrameworkOutbox<WalletDbContext>(outbox =>
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

        bus.ReceiveEndpoint("wallet-process-operation", endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(2)));
            endpoint.ConfigureConsumer<ProcessWalletOperationConsumer>(context);
        });
    });
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT Issuer Missing");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT Audience Missing");

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret Missing");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddScoped<IWalletService, WalletService.Services.WalletService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
