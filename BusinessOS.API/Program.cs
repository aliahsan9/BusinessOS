using System.Text;
using BusinessOS.API.Endpoints;
using BusinessOS.API.Middleware;
using BusinessOS.Application;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

#region SERVICES

builder.Services.AddControllers();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<CreateProductCommandValidator>();

builder.Services.AddHttpContextAccessor();

// OpenAPI + Scalar
builder.Services.AddOpenApi();

#region JWT AUTH

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]
                    ?? throw new InvalidOperationException("Jwt:Key is missing.")
                ))
        };
    });

builder.Services.AddAuthorization();

#endregion

#endregion

var app = builder.Build();

#region OPENAPI + SCALAR (MUST BE FIRST)

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options.Title = "BusinessOS API";
        options.Theme = ScalarTheme.BluePlanet;

        options.DefaultHttpClient =
            new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

#endregion

#region CORE MIDDLEWARE PIPELINE

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region ENDPOINTS

app.MapControllers();

// Minimal API endpoints
app.MapCategoryEndpoints();
app.MapProductEndpoints();

#endregion

#region TENANT MIDDLEWARE (IMPORTANT: AFTER OPENAPI + ENDPOINTS)

app.UseMiddleware<TenantMiddleware>();

#endregion

app.Run();
