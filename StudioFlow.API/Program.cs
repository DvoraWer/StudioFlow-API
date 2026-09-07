using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using StudioFlow.API.Authentication;
using StudioFlow.API.Contracts;
using StudioFlow.API.Middleware;
using StudioFlow.Core.Interfaces;
using StudioFlow.Core.Interfaces.Repositories;
using StudioFlow.Core.Interfaces.Security;
using StudioFlow.Core.Interfaces.Services;
using StudioFlow.Data.Context;
using StudioFlow.Data.Repositories;
using StudioFlow.Data.Seed;
using StudioFlow.Service.Mapping;
using StudioFlow.Service.Security;
using StudioFlow.Service.Services;

// NLog is configured before the host so a failure during startup is still logged (spec §34).
var startupLogger = LogManager.Setup().LoadConfigurationFromFile("nlog.config").GetCurrentClassLogger();
startupLogger.Info("StudioFlow API starting up.");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog becomes the provider under Microsoft.Extensions.Logging / ILogger<T>.
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // -----------------------------------------------------------------------
    // MVC + uniform validation error shape (spec §35 — model validation => 400)
    // -----------------------------------------------------------------------
    builder.Services.AddControllers();
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = context.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .SelectMany(kv => kv.Value!.Errors.Select(e => e.ErrorMessage))
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? "One or more validation errors occurred.";

            var error = new ApiError("VALIDATION_ERROR", message, context.HttpContext.GetCorrelationId());
            return new BadRequestObjectResult(error);
        };
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "StudioFlow API", Version = "v1" });

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT returned by /api/auth/login.",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        options.AddSecurityDefinition("Bearer", scheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
    });

    // -----------------------------------------------------------------------
    // EF Core / PostgreSQL (spec §3, §25) — Data is referenced for DI wiring only.
    // Connection string comes from configuration (User Secrets in development — spec §44).
    // -----------------------------------------------------------------------
    builder.Services.AddDbContext<StudioFlowDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Data access: unit of work + repositories (scoped, same lifetime as the DbContext)
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IInstructorRepository, InstructorRepository>();
    builder.Services.AddScoped<IRoomRepository, RoomRepository>();
    builder.Services.AddScoped<IClassRepository, ClassRepository>();
    builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
    builder.Services.AddScoped<IWaitlistRepository, WaitlistRepository>();

    // -----------------------------------------------------------------------
    // Service layer (spec §29) + mapping (spec §24)
    // -----------------------------------------------------------------------
    builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IClassService, ClassService>();
    builder.Services.AddScoped<IRegistrationService, RegistrationService>();
    builder.Services.AddScoped<IWaitlistService, WaitlistService>();
    builder.Services.AddScoped<IRoomService, RoomService>();
    builder.Services.AddScoped<IInstructorService, InstructorService>();
    builder.Services.AddScoped<IUserService, UserService>();

    // Stateless infrastructure
    builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
    builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

    // -----------------------------------------------------------------------
    // Authentication / authorization (spec §19, §20, §44)
    // -----------------------------------------------------------------------
    builder.Services.AddOptions<JwtOptions>()
        .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
              ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
            };

            // Emit the uniform ApiError body for auth failures (spec §31, §35).
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await WriteAuthErrorAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                        "UNAUTHORIZED", "Authentication is required to access this resource.");
                },
                OnForbidden = context =>
                    WriteAuthErrorAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                        "FORBIDDEN", "You do not have permission to perform this action.")
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // -----------------------------------------------------------------------
    // DEVELOPMENT ONLY (spec §27): migrate + idempotent seed (with real password hashes)
    // -----------------------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await db.Database.MigrateAsync();
        await SeedData.SeedAsync(db, passwordHasher);
    }

    // -----------------------------------------------------------------------
    // HTTP pipeline (spec §33): CorrelationId -> ExceptionHandling -> HTTPS
    //   -> Routing -> AuthN -> AuthZ -> Controllers
    // -----------------------------------------------------------------------
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // e.g. bad configuration, DB unreachable at startup, missing Jwt:Key.
    startupLogger.Error(ex, "StudioFlow API terminated unexpectedly during startup.");
    throw;
}
finally
{
    LogManager.Shutdown();
}

static Task WriteAuthErrorAsync(HttpContext context, int status, string code, string message)
{
    if (context.Response.HasStarted)
    {
        return Task.CompletedTask;
    }

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/json";
    var body = new ApiError(code, message, context.GetCorrelationId());
    return context.Response.WriteAsJsonAsync(body);
}

/// <summary>Exposed so WebApplicationFactory-based tests can reference the entry point.</summary>
public partial class Program;
