IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey])
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [SalesGroupId] nvarchar(450) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [SalesGroups] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Description] nvarchar(max) NULL,
    [GroupAdminId] nvarchar(450) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_SalesGroups] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SalesGroups_AspNetUsers_GroupAdminId] FOREIGN KEY ([GroupAdminId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [Customers] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(200) NOT NULL,
    [Phone] nvarchar(20) NOT NULL,
    [Company] nvarchar(200) NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(100) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Notes] nvarchar(1000) NULL,
    [ConvertedById] nvarchar(450) NOT NULL,
    [SalesGroupId] nvarchar(450) NULL,
    [ConversionDate] datetime2 NOT NULL,
    [OriginalLeadId] nvarchar(max) NOT NULL,
    [LeadCreatedDate] datetime2 NOT NULL,
    [DaysToConvert] int NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Customers_AspNetUsers_ConvertedById] FOREIGN KEY ([ConvertedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Customers_SalesGroups_SalesGroupId] FOREIGN KEY ([SalesGroupId]) REFERENCES [SalesGroups] ([Id]) ON DELETE SET NULL
);
GO

CREATE TABLE [Leads] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [Email] nvarchar(200) NOT NULL,
    [Phone] nvarchar(20) NOT NULL,
    [Company] nvarchar(200) NULL,
    [Address] nvarchar(500) NULL,
    [City] nvarchar(100) NULL,
    [State] nvarchar(100) NULL,
    [ZipCode] nvarchar(20) NULL,
    [Notes] nvarchar(1000) NULL,
    [Status] int NOT NULL,
    [AssignedToId] nvarchar(450) NOT NULL,
    [SalesGroupId] nvarchar(450) NULL,
    [CreatedDate] datetime2 NOT NULL,
    [ExpiryDate] datetime2 NOT NULL,
    [ConvertedDate] datetime2 NULL,
    [LastContactDate] datetime2 NULL,
    [IsExpired] bit NOT NULL,
    [IsExtended] bit NOT NULL,
    [ExtensionGrantedDate] datetime2 NULL,
    [ExtensionGrantedBy] nvarchar(max) NULL,
    [CreatedById] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leads_AspNetUsers_AssignedToId] FOREIGN KEY ([AssignedToId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_AspNetUsers_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Leads_SalesGroups_SalesGroupId] FOREIGN KEY ([SalesGroupId]) REFERENCES [SalesGroups] ([Id]) ON DELETE SET NULL
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE INDEX [IX_AspNetUsers_SalesGroupId] ON [AspNetUsers] ([SalesGroupId]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_Customers_ConversionDate] ON [Customers] ([ConversionDate]);
GO

CREATE INDEX [IX_Customers_ConvertedById] ON [Customers] ([ConvertedById]);
GO

CREATE INDEX [IX_Customers_Email] ON [Customers] ([Email]);
GO

CREATE INDEX [IX_Customers_Phone] ON [Customers] ([Phone]);
GO

CREATE INDEX [IX_Customers_SalesGroupId] ON [Customers] ([SalesGroupId]);
GO

CREATE INDEX [IX_Leads_AssignedToId] ON [Leads] ([AssignedToId]);
GO

CREATE INDEX [IX_Leads_CreatedById] ON [Leads] ([CreatedById]);
GO

CREATE INDEX [IX_Leads_CreatedDate] ON [Leads] ([CreatedDate]);
GO

CREATE INDEX [IX_Leads_Email] ON [Leads] ([Email]);
GO

CREATE INDEX [IX_Leads_ExpiryDate] ON [Leads] ([ExpiryDate]);
GO

CREATE INDEX [IX_Leads_Phone] ON [Leads] ([Phone]);
GO

CREATE INDEX [IX_Leads_SalesGroupId] ON [Leads] ([SalesGroupId]);
GO

CREATE INDEX [IX_Leads_Status] ON [Leads] ([Status]);
GO

CREATE INDEX [IX_SalesGroups_GroupAdminId] ON [SalesGroups] ([GroupAdminId]);
GO

CREATE UNIQUE INDEX [IX_SalesGroups_Name] ON [SalesGroups] ([Name]);
GO

ALTER TABLE [AspNetUserClaims] ADD CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AspNetUserLogins] ADD CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;
GO

ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_SalesGroups_SalesGroupId] FOREIGN KEY ([SalesGroupId]) REFERENCES [SalesGroups] ([Id]) ON DELETE SET NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251120062221_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

