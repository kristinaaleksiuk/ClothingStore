using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json.Serialization;
using ClothingStore.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using static ClothingStore.Models.User;

var builder = WebApplication.CreateBuilder(args);

// Выключить циклы
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Подключение БД
builder.Services.AddDbContext<ClothingStoreContext>();

var hasher = new PasswordHasher<User>();

var key = new SymmetricSecurityKey(
    RandomNumberGenerator.GetBytes(32));

// Параметры токена 
var validation = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = "JwtDemo",

    ValidateAudience = true,
    ValidAudience = "JwtDemoApi",

    ValidateIssuerSigningKey = true,
    IssuerSigningKey = key,

    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,

    NameClaimType = "Login",
    RoleClaimType = "Role"
};

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = validation;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Регистрация юзера 
app.MapPost("/api/users", async (ClothingStoreContext context, User user) =>
{
    user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
    context.Users.Add(user);
    await context.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapGet("/api/users", (ClothingStoreContext context) => Results.Json(context.Users));

// Смотрит логин и пароль пользователя, возвращает токен
app.MapPost("/api/auth/login", async (ClothingStoreContext context, LoginRequest request) =>
{
    var users = await context.Users.ToListAsync();

    var user = users.FirstOrDefault(
        a => a.Login == request.Login);

    if (user == null ||
    string.IsNullOrWhiteSpace(request.Password))
        return Results.Challenge();

    var result = hasher.VerifyHashedPassword(
        user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Challenge();

    return Results.Ok(new
    {
        access_token = CreateToken(user),
        token_type = "Bearer"
    });
}).AllowAnonymous();

app.Run();

string CreateToken(User account)
{
    var claims = new[]
    {
        new Claim("sub", account.Id.ToString()),
        new Claim("name", account.Login),
        new Claim("role", "User"),

    };

    var token = new JwtSecurityToken(
        issuer: "JwtDemo",
        audience: "JwtDemoApi",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}