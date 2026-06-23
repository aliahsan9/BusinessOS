using System.Text;
using BusinessOS.API.Endpoints;
using BusinessOS.API.Middleware;
using BusinessOS.Application;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Services;
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

builder.Services.AddOpenApi();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped< 
    ICurrentUserService,
    CurrentUserService>();

builder.Services.AddHttpContextAccessor();

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

var app = builder.Build();

#region OPENAPI + SCALAR

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

#region MIDDLEWARE PIPELINE

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// MUST RUN BEFORE ENDPOINTS
app.UseMiddleware<TenantMiddleware>();

#endregion

#region ENDPOINTS

app.MapControllers();
app.MapAuthEndpoints();
app.MapCategoryEndpoints();
app.MapProductEndpoints();

#endregion

app.Run();
