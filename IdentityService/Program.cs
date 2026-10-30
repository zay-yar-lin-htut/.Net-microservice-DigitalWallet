using System.Text;
using IdentityService.Data;
using IdentityService.Entities;
using IdentityService.Middleware;
using IdentityService.Messaging;
using IdentityService.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(context.Configuration["Seq:ServerUrl"] ?? "http://seq:5341"));

var connectionString =
    builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Identity Database Connection String Missing");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<RegisterUserConsumer>();
    configurator.AddEntityFrameworkOutbox<IdentityDbContext>(outbox =>
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

        bus.ReceiveEndpoint("identity-register-user", endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(2)));
            endpoint.ConfigureConsumer<RegisterUserConsumer>(context);
        });
    });
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

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
builder.Services.AddScoped<IIdentityService, IdentityService.Services.IdentityService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
