using ClothingStore.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Отключить циклы
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// Подключение БД
builder.Services.AddDbContext<ClothingStoreContext>();

var hasher = new PasswordHasher<User>();

// Получаем постоянный секретный ключ из appsettings.json
var jwtSecret = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JWT Key должен содержать не менее 32 байт."
    );
}

var key = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSecret)
);
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

    NameClaimType = "name",
    RoleClaimType = "role"
};

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = validation;
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

string managerRole = "Manager";
string userRole = "User";
string cancelledStatus = "Cancelled";

// Регистрация нового пользователя
app.MapPost("/api/users", async (ClothingStoreContext context, RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.BadRequest(new { message = "Логин, пароль и телефон обязательны" });
    }

    var exists = await context.Users.AnyAsync(u => u.Login == request.Login);
    if (exists)
    {
        return Results.Conflict(new { message = "Такой логин уже занят" });
    }

    var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == userRole);
    if (role == null)
    {
        return Results.Problem("В таблице Roles нет роли User");
    }

    var user = new User();
    user.Login = request.Login;
    user.Phone = request.Phone;
    user.DeliveryAddress = request.DeliveryAddress;
    user.RoleId = role.Id;
    user.PasswordHash = hasher.HashPassword(user, request.Password);

    context.Users.Add(user);
    await context.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Login });
}).AllowAnonymous();

// Авторизация пользователя
app.MapPost("/api/auth/login", async (ClothingStoreContext context, LoginRequest request) =>
{
    var user = await context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Login == request.Login);

    if (user == null || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Unauthorized();
    }

    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        access_token = CreateToken(user),
        token_type = "Bearer"
    });
}).AllowAnonymous();

// Получить список всей одежды
app.MapGet("/api/products", async (ClothingStoreContext context) =>
{
    var products = await context.Products.Select(p => new { p.Id, p.Name, p.Price, p.IsAvailable, Sizes = p.ProductSizes.Select(ps => new { ps.SizeId, Size = ps.Size.Label, ps.Stock }).ToList() }).ToListAsync();
    return Results.Ok(products);
}).AllowAnonymous();

// Поиск одежды по названию
app.MapGet("/api/products/search", async (ClothingStoreContext context, string name) =>
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new { message = "Введите название для поиска" });
    }
    var products = await context.Products.Where(p => p.Name.Contains(name.Trim())).Select(p => new { p.Id, p.Name, p.Price, p.IsAvailable, Sizes = p.ProductSizes.Select(ps => new { ps.SizeId, Size = ps.Size.Label, ps.Stock }).ToList() }).ToListAsync();
    return Results.Ok(products);
}).AllowAnonymous();

// Получить список всех пользователей (только менеджер)
app.MapGet("/api/users", async (ClothingStoreContext context) =>
{
    var users = await context.Users.Select(u => new { u.Id, u.Login, u.Phone, u.DeliveryAddress, Role = u.Role.Name }).ToListAsync();
    return Results.Ok(users);
}).RequireAuthorization(p => p.RequireRole("Manager"));

// Создать заказ (только авторизованный пользователь)
app.MapPost("/api/orders", async (ClothingStoreContext context, ClaimsPrincipal user, CreateOrderRequest request) =>
{
    if (!int.TryParse(user.FindFirstValue("sub"), out var userId)) 
    {
        return Results.Unauthorized();
    }

    if (request.Items == null || request.Items.Count == 0) 
    {
        return Results.BadRequest(new { message = "Заказ пустой" });
    }

    var firstStatus = await context.OrderStatuses.FirstOrDefaultAsync(o => o.Name == "New"); 

    if (firstStatus == null)
    {
        return Results.Problem("Таблица OrderStatuses пустая");
    }

    await using var transaction = await context.Database.BeginTransactionAsync();
    var order = new Order
    { 
        UserId = userId,
        StatusId = firstStatus.Id 
    };

    foreach(var item in request.Items)
    {
        if (item.Quantity <= 0) 
        {
            return Results.BadRequest(new { message = "Количество должно быть больше 0" });
        }

        var productsSize = await context.ProductSizes.Include(ps => ps.Product).FirstOrDefaultAsync(ps => ps.ProductId == item.ProductId && ps.SizeId == item.SizeId);

        if (productsSize==null)
        {
            return Results.NotFound(new { message = "Выбранный размер не найден" });
        }

        if (!productsSize.Product.IsAvailable)
        {
            return Results.BadRequest(new { message = $"Товар {productsSize.Product.Name} снят с продажи" });
        }

        if (productsSize.Stock < item.Quantity)
        {
            return Results.BadRequest(new { message = $"Недостаточно товара {productsSize.Product.Name} на складе" });
        }

        productsSize.Stock -= item.Quantity;
        order.OrderItems.Add(new OrderItem
        {
            ProductSizeId = productsSize.Id,
            Quantity = item.Quantity,
            UnitPrice = productsSize.Product.Price
        });
    }
    context.Orders.Add(order);

    await context.SaveChangesAsync();
    await transaction.CommitAsync();

    var savedOrder = await OrdersWithDetails(context).FirstAsync(o => o.Id == order.Id);
    return Results.Created($"/api/orders/{order.Id}", MakeOrderResult(savedOrder));
}).RequireAuthorization();

// Получить список заказов по статусу (только менеджер видит все, клиент только свои)
app.MapGet("/api/orders/by-status/{statusId}", async (ClothingStoreContext context, ClaimsPrincipal user, int statusId) =>
{
    var status = await context.OrderStatuses.FindAsync(statusId);
    if (status == null)
    {
        return Results.NotFound(new { message = "Такого статуса нет" });
    }

    var query = OrdersWithDetails(context).Where(o => o.StatusId == statusId);

    if (user.IsInRole(managerRole) == false)
    {
        if(!int.TryParse(user.FindFirstValue("sub"), out var userId))
        {
            return Results.Unauthorized();
        }
        query = query.Where(o => o.UserId == userId);
    }

    var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

    var result = new List<object>();
    foreach (var order in orders)
    {
        result.Add(MakeOrderResult(order));
    }

    return Results.Ok(result);
}).RequireAuthorization();


// Получить список заказов клиента (клиент видит только свои, менеджер любые)
app.MapGet("/api/orders/by-user/{userId}", async (ClothingStoreContext context, ClaimsPrincipal user, int userId) =>
{
    if(!int.TryParse(user.FindFirstValue("sub"), out var currentUserId))
    {
        return Results.Unauthorized();
    }

    if (user.IsInRole(managerRole) == false && currentUserId != userId)
    {
        return Results.Forbid();
    }
    var orders = await OrdersWithDetails(context).Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).ToListAsync();

    var result = new List<object>();
    foreach (var order in orders)
    {
        result.Add(MakeOrderResult(order));
    }

    return Results.Ok(result);
}).RequireAuthorization();


// Изменить статус заказа (только менеджер)
app.MapPatch("/api/orders/{id}/status", async (ClothingStoreContext context, int id, int statusId) =>
{
    var newStatus = await context.OrderStatuses.FindAsync(statusId);
    if (newStatus == null)
    {
        return Results.BadRequest(new { message = "Такого статуса нет" });
    }

    var order = await context.Orders.Include(o => o.Status).Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);

    if (order == null)
    {
        return Results.NotFound(new { message = "Заказ не найден" });
    }

    if (order.Status.Name == cancelledStatus)
    {
        return Results.BadRequest(new { message = "Отменённый заказ нельзя изменить" });
    }

    if (newStatus.Name == cancelledStatus)
    {
        foreach (var item in order.OrderItems)
        {
            var productSize = await context.ProductSizes.FindAsync(item.ProductSizeId);
            if (productSize != null)
            {
                productSize.Stock += item.Quantity;
            }
        }
    }

    order.StatusId = newStatus.Id;
    await context.SaveChangesAsync();

    return Results.Ok(new { order.Id, Status = newStatus.Name });
}).RequireAuthorization(p => p.RequireRole("Manager"));


// Изменить количество товара в размере (только менеджер)
app.MapPatch("/api/products/{productId}/sizes/{sizeId}/stock", async (ClothingStoreContext context, int productId, int sizeId, int stock) =>
{
    if (stock < 0)
    {
        return Results.BadRequest(new { message = "Количество не может быть отрицательным" });
    }

    var productSize = await context.ProductSizes.FirstOrDefaultAsync(p => p.ProductId == productId && p.SizeId == sizeId);

    if (productSize == null)
    {
        return Results.NotFound(new { message = "Такой товар с таким размером не найден" });
    }

    productSize.Stock = stock;
    await context.SaveChangesAsync();

    return Results.Ok(new { productId, sizeId, productSize.Stock });
}).RequireAuthorization(p => p.RequireRole("Manager"));


// Снять товар с продажи (только менеджер)
app.MapPost("/api/products/{id}/deactivate", async (ClothingStoreContext context, int id) =>
{
    var product = await context.Products.FindAsync(id);
    if (product == null)
    {
        return Results.NotFound(new { message = "Товар не найден" });
    }

    product.IsAvailable = false;
    await context.SaveChangesAsync();

    return Results.Ok(new { product.Id, product.IsAvailable });
}).RequireAuthorization(p => p.RequireRole("Manager"));


// Вернуть товар в продажу (только менеджер)
app.MapPost("/api/products/{id}/activate", async (ClothingStoreContext context, int id) =>
{
    var product = await context.Products.FindAsync(id);
    if (product == null)
    {
        return Results.NotFound(new { message = "Товар не найден" });
    }

    product.IsAvailable = true;
    await context.SaveChangesAsync();

    return Results.Ok(new { product.Id, product.IsAvailable });
}).RequireAuthorization(p => p.RequireRole("Manager"));

app.Run();
string CreateToken(User account)
{
    var claims = new[]
    {
        new Claim("sub", account.Id.ToString()),
        new Claim("name", account.Login),
        new Claim("role", account.Role.Name),

    };

    var token = new JwtSecurityToken(
        issuer: "JwtDemo",
        audience: "JwtDemoApi",
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(60),
        signingCredentials: new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256));

    return new JwtSecurityTokenHandler().WriteToken(token);
}

IQueryable<Order> OrdersWithDetails(ClothingStoreContext context)
{
    return context.Orders.Include(o => o.Status).Include(o => o.OrderItems).ThenInclude(i => i.ProductSize).ThenInclude(i => i.Product).Include(o => o.OrderItems).ThenInclude(i => i.ProductSize).ThenInclude(i => i.Size);
}
object MakeOrderResult(Order order)
{
    var items = new List<object>();
    decimal total = 0;
    foreach (var item in order.OrderItems)
    {
        items.Add(new
        {
            ProductId = item.ProductSize.ProductId,
            ProductName = item.ProductSize.Product.Name,
            Size = item.ProductSize.Size.Label,
            item.Quantity,
            item.UnitPrice
        });

        total = total + item.Quantity * item.UnitPrice;
    }
    return new
    {
        order.Id,
        order.UserId,
        order.CreatedAt,
        Status = order.Status.Name,
        Total = total,
        Items = items
    };
}
public class RegisterRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
}

public class OrderItemRequest
{
    public int ProductId { get; set; }
    public int SizeId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest
{
    public List<OrderItemRequest> Items { get; set; } = new ();
}