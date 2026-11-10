using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using HospitalManagement.Api.Authentication;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.Constants;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Hubs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.SignalR;
using HospitalManagement.Api.Middleware;
using HospitalManagement.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (no secrets in source control) ----
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. " +
        "Set it with 'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"' " +
        "or the ConnectionStrings__DefaultConnection environment variable.");
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32 ||
    string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
    string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
    jwtOptions.AccessTokenMinutes < 1 || jwtOptions.RefreshTokenDays < 1)
{
    throw new InvalidOperationException(
        "JWT configuration is invalid. Set a 32-byte Jwt:Key, Jwt:Issuer, Jwt:Audience, " +
        "and positive access-token and refresh-token lifetimes using user-secrets or environment variables.");
}

// ---- Services ----
// ---- Notifications: staging in the business transaction, SignalR push after commit, scheduled derived alerts ----
builder.Services.AddSignalR()
    // Same wire format as the REST API: enums as strings ("AppointmentBooked"), so one TypeScript type serves both channels.
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<INotificationBroadcaster, SignalRNotificationBroadcaster>();
builder.Services.AddSingleton<NotificationBroadcastInterceptor>();
builder.Services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<INotificationAlertService, NotificationAlertService>();
builder.Services.AddHostedService<NotificationSchedulerService>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => options
    .UseNpgsql(connectionString)
    .AddInterceptors(serviceProvider.GetRequiredService<NotificationBroadcastInterceptor>()));
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IConsultationService, ConsultationService>();
builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
builder.Services.AddScoped<IPharmacyService, PharmacyService>();
builder.Services.AddScoped<IPharmacyStockService, PharmacyStockService>();
builder.Services.AddScoped<IDispensingService, DispensingService>();
builder.Services.AddScoped<ILaboratoryService, LaboratoryService>();
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
builder.Services.AddScoped<IClinicSetupService, ClinicSetupService>();
builder.Services.AddScoped<INursingService, NursingService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IStaffAccountService, StaffAccountService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            // Browsers cannot set an Authorization header on a WebSocket / SSE handshake, so SignalR sends the access
            // token in the query string. It is honoured ONLY for the notification hub path.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(NotificationHub.Path))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Failure("Authentication is required.", traceId: context.HttpContext.TraceIdentifier));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Failure("You do not have permission to perform this action.", traceId: context.HttpContext.TraceIdentifier));
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Model-binding / validation failures use the same envelope as every other error.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrEmpty(entry.Key) ? error.ErrorMessage : $"{entry.Key}: {error.ErrorMessage}"))
                .ToList();

            return new BadRequestObjectResult(
                ApiResponse.Failure("One or more validation errors occurred.", errors, context.HttpContext.TraceIdentifier));
        };
    });

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicies.Frontend, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Access token returned by the authentication endpoints."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ---- Pipeline ----
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // System initialisation: always runs and is idempotent.
    await RbacSeeder.SeedAsync(dbContext);

    // Demo data (sample department, doctor, patient, lab tests, ward, room, beds) is OFF unless SeedDemoData=true.
    // Real hospital data is created through the application UI.
    if (app.Configuration.GetValue<bool>("SeedDemoData"))
    {
        await ClinicSeeder.SeedAsync(dbContext);
        await LabSeeder.SeedAsync(dbContext);
        await AdmissionSeeder.SeedAsync(dbContext);
    }

    await BootstrapAdminSeeder.SeedAsync(app.Configuration, dbContext);

    // Repair: Doctor/Nurse logins created earlier without a profile row (so dropdowns and tables were empty).
    await StaffAccountService.BackfillProfilesAsync(dbContext);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicies.Frontend);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>) in Phase 15.
public partial class Program { }
