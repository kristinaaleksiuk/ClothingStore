USE [master];
GO

IF DB_ID(N'ClothingStore') IS NOT NULL
BEGIN
    ALTER DATABASE [ClothingStore] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [ClothingStore];
END
GO

CREATE DATABASE [ClothingStore];
GO

USE [ClothingStore];
GO

-- ===== Справочники =====
CREATE TABLE dbo.Roles (
    Id   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name nvarchar(50) NOT NULL CONSTRAINT UQ_Roles_Name UNIQUE
);

CREATE TABLE dbo.ClothingTypes (
    Id   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClothingTypes PRIMARY KEY,
    Name nvarchar(100) NOT NULL CONSTRAINT UQ_ClothingTypes_Name UNIQUE
);

CREATE TABLE dbo.OrderStatuses (
    Id   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderStatuses PRIMARY KEY,
    Name nvarchar(50) NOT NULL CONSTRAINT UQ_OrderStatuses_Name UNIQUE
);

CREATE TABLE dbo.Sizes (
    Id         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sizes PRIMARY KEY,
    Label      nvarchar(10) NOT NULL,
    NumberSize int NOT NULL,
    CONSTRAINT UQ_Sizes UNIQUE (Label, NumberSize)
);

-- ===== Пользователи =====
CREATE TABLE dbo.Users (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Login           nvarchar(50)  NOT NULL CONSTRAINT UQ_Users_Login UNIQUE,
    PasswordHash    nvarchar(255) NOT NULL,
    Phone           nvarchar(20)  NOT NULL,
    DeliveryAddress nvarchar(300) NULL,
    RoleId          int NOT NULL CONSTRAINT FK_Users_Roles REFERENCES dbo.Roles(Id),
    CreatedAt       datetime2(0) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (sysutcdatetime())
);

-- ===== Товары =====
CREATE TABLE dbo.Products (
    Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name           nvarchar(200) NOT NULL,
    Price          decimal(10,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price >= 0),
    Description    nvarchar(max) NULL,
    PurchaseCount  int NOT NULL CONSTRAINT DF_Products_PurchaseCount DEFAULT (0)
                       CONSTRAINT CK_Products_PurchaseCount CHECK (PurchaseCount >= 0),
    ClothingTypeId int NOT NULL CONSTRAINT FK_Products_ClothingTypes REFERENCES dbo.ClothingTypes(Id),
    IsAvailable    bit NOT NULL CONSTRAINT DF_Products_IsAvailable DEFAULT (1)
);
CREATE NONCLUSTERED INDEX IX_Products_Name ON dbo.Products(Name);

CREATE TABLE dbo.ProductSizes (
    Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductSizes PRIMARY KEY,
    ProductId int NOT NULL CONSTRAINT FK_ProductSizes_Products REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    SizeId    int NOT NULL CONSTRAINT FK_ProductSizes_Sizes    REFERENCES dbo.Sizes(Id),
    Stock     int NOT NULL CONSTRAINT DF_ProductSizes_Stock DEFAULT (0)
                  CONSTRAINT CK_ProductSizes_Stock CHECK (Stock >= 0),
    CONSTRAINT UQ_ProductSizes UNIQUE (ProductId, SizeId)
);

-- ===== Заказы =====
CREATE TABLE dbo.Orders (
    Id        int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    UserId    int NOT NULL CONSTRAINT FK_Orders_Users    REFERENCES dbo.Users(Id),
    StatusId  int NOT NULL CONSTRAINT FK_Orders_Statuses REFERENCES dbo.OrderStatuses(Id),
    CreatedAt datetime2(0) NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT (sysutcdatetime())
);
CREATE NONCLUSTERED INDEX IX_Orders_UserId   ON dbo.Orders(UserId);
CREATE NONCLUSTERED INDEX IX_Orders_StatusId ON dbo.Orders(StatusId);

CREATE TABLE dbo.OrderItems (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId       int NOT NULL CONSTRAINT FK_OrderItems_Orders       REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    ProductSizeId int NOT NULL CONSTRAINT FK_OrderItems_ProductSizes REFERENCES dbo.ProductSizes(Id),
    Quantity      int NOT NULL CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0),
    UnitPrice     decimal(10,2) NOT NULL
);
CREATE NONCLUSTERED INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
GO
