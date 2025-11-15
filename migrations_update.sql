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
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [PhotoUrl] nvarchar(max) NULL,
    [DateOfBirth] datetime2 NULL,
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

CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name_en] nvarchar(150) NOT NULL,
    [Name_ar] nvarchar(max) NULL,
    [Name_it] nvarchar(max) NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Levels] (
    [Id] int NOT NULL IDENTITY,
    [Title_en] nvarchar(200) NOT NULL,
    [Title_ar] nvarchar(max) NULL,
    [Title_it] nvarchar(max) NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_Levels] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Admins] (
    [Id] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_Admins] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Admins_AspNetUsers_Id] FOREIGN KEY ([Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Students] (
    [Id] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_Students] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Students_AspNetUsers_Id] FOREIGN KEY ([Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Teachers] (
    [Id] nvarchar(450) NOT NULL,
    [JobTitle] nvarchar(150) NOT NULL,
    [ShortBio] nvarchar(2000) NULL,
    [CVUrl] nvarchar(500) NULL,
    [IntroVideoUrl] nvarchar(200) NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Teachers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Teachers_AspNetUsers_Id] FOREIGN KEY ([Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Curricula] (
    [Id] int NOT NULL IDENTITY,
    [LevelId] int NOT NULL,
    [Title_en] nvarchar(max) NULL,
    [Title_ar] nvarchar(max) NULL,
    [Title_it] nvarchar(max) NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_Curricula] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Curricula_Levels_LevelId] FOREIGN KEY ([LevelId]) REFERENCES [Levels] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PrivateCourses] (
    [Id] int NOT NULL IDENTITY,
    [TeacherId] nvarchar(450) NULL,
    [CategoryId] int NOT NULL,
    [Title] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [PriceLabel] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_PrivateCourses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PrivateCourses_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PrivateCourses_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
);

CREATE TABLE [TeacherAvailableSlots] (
    [Id] int NOT NULL IDENTITY,
    [TeacherId] nvarchar(450) NULL,
    [StartUtc] datetime2 NOT NULL,
    [EndUtc] datetime2 NOT NULL,
    [MeetUrl] nvarchar(max) NULL,
    [IsBooked] bit NOT NULL,
    CONSTRAINT [PK_TeacherAvailableSlots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TeacherAvailableSlots_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
);

CREATE TABLE [SchoolModules] (
    [Id] int NOT NULL IDENTITY,
    [CurriculumId] int NOT NULL,
    [Title_en] nvarchar(max) NULL,
    [Title_ar] nvarchar(max) NULL,
    [Title_it] nvarchar(max) NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_SchoolModules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SchoolModules_Curricula_CurriculumId] FOREIGN KEY ([CurriculumId]) REFERENCES [Curricula] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PrivateModules] (
    [Id] int NOT NULL IDENTITY,
    [Title_en] nvarchar(250) NOT NULL,
    [Title_ar] nvarchar(250) NULL,
    [Title_it] nvarchar(250) NULL,
    [Description] nvarchar(2000) NULL,
    [Order] int NOT NULL DEFAULT 0,
    [PrivateCourseId] int NOT NULL,
    CONSTRAINT [PK_PrivateModules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PrivateModules_PrivateCourses_PrivateCourseId] FOREIGN KEY ([PrivateCourseId]) REFERENCES [PrivateCourses] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PurchaseRequests] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] nvarchar(450) NULL,
    [RecordedCourseId] int NOT NULL,
    [RequestDateUtc] datetime2 NOT NULL,
    [Status] int NOT NULL,
    [AdminNote] nvarchar(max) NULL,
    CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PurchaseRequests_PrivateCourses_RecordedCourseId] FOREIGN KEY ([RecordedCourseId]) REFERENCES [PrivateCourses] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PurchaseRequests_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id])
);

CREATE TABLE [Bookings] (
    [Id] int NOT NULL IDENTITY,
    [StudentId] nvarchar(450) NULL,
    [TeacherId] nvarchar(450) NULL,
    [SlotId] int NULL,
    [RequestedDateUtc] datetime2 NOT NULL,
    [MeetUrl] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bookings_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]),
    CONSTRAINT [FK_Bookings_TeacherAvailableSlots_SlotId] FOREIGN KEY ([SlotId]) REFERENCES [TeacherAvailableSlots] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Bookings_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id])
);

CREATE TABLE [SchoolLessons] (
    [Id] int NOT NULL IDENTITY,
    [ModuleId] int NOT NULL,
    [Title_en] nvarchar(max) NULL,
    [Title_ar] nvarchar(max) NULL,
    [Title_it] nvarchar(max) NULL,
    [YouTubeVideoId] nvarchar(max) NULL,
    [IsFree] bit NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_SchoolLessons] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SchoolLessons_SchoolModules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [SchoolModules] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PrivateLessons] (
    [Id] int NOT NULL IDENTITY,
    [PrivateCourseId] int NOT NULL,
    [PrivateModuleId] int NULL,
    [Title] nvarchar(300) NOT NULL,
    [YouTubeVideoId] nvarchar(100) NULL,
    [Order] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_PrivateLessons] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PrivateLessons_PrivateCourses_PrivateCourseId] FOREIGN KEY ([PrivateCourseId]) REFERENCES [PrivateCourses] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PrivateLessons_PrivateModules_PrivateModuleId] FOREIGN KEY ([PrivateModuleId]) REFERENCES [PrivateModules] ([Id])
);

CREATE TABLE [FileResources] (
    [Id] int NOT NULL IDENTITY,
    [SchoolLessonId] int NULL,
    [PrivateLessonId] int NULL,
    [FileUrl] nvarchar(max) NULL,
    [Name] nvarchar(max) NULL,
    [FileType] nvarchar(max) NULL,
    CONSTRAINT [PK_FileResources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FileResources_PrivateLessons_PrivateLessonId] FOREIGN KEY ([PrivateLessonId]) REFERENCES [PrivateLessons] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_FileResources_SchoolLessons_SchoolLessonId] FOREIGN KEY ([SchoolLessonId]) REFERENCES [SchoolLessons] ([Id]) ON DELETE SET NULL
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Bookings_SlotId] ON [Bookings] ([SlotId]);

CREATE INDEX [IX_Bookings_StudentId] ON [Bookings] ([StudentId]);

CREATE INDEX [IX_Bookings_TeacherId] ON [Bookings] ([TeacherId]);

CREATE INDEX [IX_Curricula_LevelId] ON [Curricula] ([LevelId]);

CREATE INDEX [IX_FileResources_PrivateLessonId] ON [FileResources] ([PrivateLessonId]);

CREATE INDEX [IX_FileResources_SchoolLessonId] ON [FileResources] ([SchoolLessonId]);

CREATE INDEX [IX_PrivateCourses_CategoryId] ON [PrivateCourses] ([CategoryId]);

CREATE INDEX [IX_PrivateCourses_TeacherId] ON [PrivateCourses] ([TeacherId]);

CREATE INDEX [IX_PrivateLessons_PrivateCourseId] ON [PrivateLessons] ([PrivateCourseId]);

CREATE INDEX [IX_PrivateLessons_PrivateModuleId] ON [PrivateLessons] ([PrivateModuleId]);

CREATE INDEX [IX_PrivateModules_PrivateCourseId] ON [PrivateModules] ([PrivateCourseId]);

CREATE INDEX [IX_PurchaseRequests_RecordedCourseId] ON [PurchaseRequests] ([RecordedCourseId]);

CREATE INDEX [IX_PurchaseRequests_StudentId] ON [PurchaseRequests] ([StudentId]);

CREATE INDEX [IX_SchoolLessons_ModuleId] ON [SchoolLessons] ([ModuleId]);

CREATE INDEX [IX_SchoolModules_CurriculumId] ON [SchoolModules] ([CurriculumId]);

CREATE INDEX [IX_TeacherAvailableSlots_TeacherId] ON [TeacherAvailableSlots] ([TeacherId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250831210245_InitialCreate', N'9.0.0');

ALTER TABLE [AspNetUsers] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250903203328_AddIsDeletedToApplicationUser', N'9.0.0');

ALTER TABLE [FileResources] DROP CONSTRAINT [FK_FileResources_PrivateLessons_PrivateLessonId];

ALTER TABLE [FileResources] DROP CONSTRAINT [FK_FileResources_SchoolLessons_SchoolLessonId];

ALTER TABLE [PurchaseRequests] DROP CONSTRAINT [FK_PurchaseRequests_PrivateCourses_RecordedCourseId];

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SchoolModules]') AND [c].[name] = N'Title_ar');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [SchoolModules] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [SchoolModules] DROP COLUMN [Title_ar];

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SchoolModules]') AND [c].[name] = N'Title_en');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [SchoolModules] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [SchoolModules] DROP COLUMN [Title_en];

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SchoolModules]') AND [c].[name] = N'Title_it');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [SchoolModules] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [SchoolModules] DROP COLUMN [Title_it];

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SchoolLessons]') AND [c].[name] = N'Title_ar');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [SchoolLessons] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [SchoolLessons] DROP COLUMN [Title_ar];

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PrivateModules]') AND [c].[name] = N'Title_ar');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PrivateModules] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [PrivateModules] DROP COLUMN [Title_ar];

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PrivateModules]') AND [c].[name] = N'Title_it');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PrivateModules] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [PrivateModules] DROP COLUMN [Title_it];

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Levels]') AND [c].[name] = N'Title_ar');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Levels] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [Levels] DROP COLUMN [Title_ar];

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Levels]') AND [c].[name] = N'Title_it');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Levels] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Levels] DROP COLUMN [Title_it];

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Curricula]') AND [c].[name] = N'Title_ar');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Curricula] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [Curricula] DROP COLUMN [Title_ar];

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'Name_ar');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [Categories] DROP COLUMN [Name_ar];

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'Name_it');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Categories] DROP COLUMN [Name_it];

EXEC sp_rename N'[SchoolLessons].[Title_it]', N'VideoUrl', 'COLUMN';

EXEC sp_rename N'[SchoolLessons].[Title_en]', N'Description', 'COLUMN';

EXEC sp_rename N'[PurchaseRequests].[RecordedCourseId]', N'PrivateCourseId', 'COLUMN';

EXEC sp_rename N'[PurchaseRequests].[IX_PurchaseRequests_RecordedCourseId]', N'IX_PurchaseRequests_PrivateCourseId', 'INDEX';

EXEC sp_rename N'[PrivateModules].[Title_en]', N'Title', 'COLUMN';

EXEC sp_rename N'[PrivateCourses].[IsActive]', N'IsPublished', 'COLUMN';

EXEC sp_rename N'[Levels].[Title_en]', N'Name', 'COLUMN';

EXEC sp_rename N'[Curricula].[Title_it]', N'Description', 'COLUMN';

EXEC sp_rename N'[Curricula].[Title_en]', N'CoverImageUrl', 'COLUMN';

EXEC sp_rename N'[Categories].[Name_en]', N'Name', 'COLUMN';

ALTER TABLE [SchoolModules] ADD [Title] nvarchar(200) NOT NULL DEFAULT N'';

ALTER TABLE [SchoolLessons] ADD [Title] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [PurchaseRequests] ADD [Amount] nvarchar(max) NULL;

ALTER TABLE [PrivateLessons] ADD [VideoUrl] nvarchar(max) NULL;

ALTER TABLE [PrivateCourses] ADD [CoverImageUrl] nvarchar(max) NULL;

ALTER TABLE [Curricula] ADD [Title] nvarchar(200) NOT NULL DEFAULT N'';

DECLARE @var11 sysname;
SELECT @var11 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'PhotoUrl');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var11 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [PhotoUrl] nvarchar(500) NULL;

DECLARE @var12 sysname;
SELECT @var12 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'IsDeleted');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var12 + '];');
ALTER TABLE [AspNetUsers] ADD DEFAULT CAST(0 AS bit) FOR [IsDeleted];

DECLARE @var13 sysname;
SELECT @var13 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'FullName');
IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var13 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [FullName] nvarchar(200) NOT NULL;

DECLARE @var14 sysname;
SELECT @var14 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'DateOfBirth');
IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var14 + '];');
ALTER TABLE [AspNetUsers] ALTER COLUMN [DateOfBirth] date NULL;

CREATE INDEX [IX_AspNetUsers_Email] ON [AspNetUsers] ([Email]);

CREATE INDEX [IX_AspNetUsers_FullName] ON [AspNetUsers] ([FullName]);

ALTER TABLE [FileResources] ADD CONSTRAINT [FK_FileResource_PrivateLesson] FOREIGN KEY ([PrivateLessonId]) REFERENCES [PrivateLessons] ([Id]) ON DELETE SET NULL;

ALTER TABLE [FileResources] ADD CONSTRAINT [FK_FileResource_SchoolLesson] FOREIGN KEY ([SchoolLessonId]) REFERENCES [SchoolLessons] ([Id]) ON DELETE SET NULL;

ALTER TABLE [PurchaseRequests] ADD CONSTRAINT [FK_PurchaseRequests_PrivateCourses_PrivateCourseId] FOREIGN KEY ([PrivateCourseId]) REFERENCES [PrivateCourses] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250912074033_FixFileResourceFk', N'9.0.0');

ALTER TABLE [FileResources] ADD [StorageKey] nvarchar(1000) NULL;


            UPDATE FileResources
            SET StorageKey = FileUrl
            WHERE StorageKey IS NULL AND FileUrl IS NOT NULL
             

CREATE TABLE [CourseModerationLogs] (
    [Id] int NOT NULL IDENTITY,
    [PrivateCourseId] int NOT NULL,
    [AdminId] nvarchar(450) NULL,
    [Action] nvarchar(64) NULL,
    [Note] nvarchar(4000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_CourseModerationLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CourseModerationLogs_PrivateCourses_PrivateCourseId] FOREIGN KEY ([PrivateCourseId]) REFERENCES [PrivateCourses] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_CourseModerationLogs_AdminId] ON [CourseModerationLogs] ([AdminId]);

CREATE INDEX [IX_CourseModerationLogs_CreatedAtUtc] ON [CourseModerationLogs] ([CreatedAtUtc]);

CREATE INDEX [IX_CourseModerationLogs_PrivateCourseId] ON [CourseModerationLogs] ([PrivateCourseId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250918091126_AddCourseModerationLogandStorageKeyProp', N'9.0.0');

ALTER TABLE [PrivateCourses] ADD [IsPublishRequested] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250923182451_AddpublishRequestPropertytoPCourses', N'9.0.0');

CREATE TABLE [BookingModerationLogs] (
    [Id] int NOT NULL IDENTITY,
    [BookingId] int NOT NULL,
    [ActorId] nvarchar(max) NULL,
    [Action] nvarchar(150) NOT NULL,
    [Note] nvarchar(2000) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_BookingModerationLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BookingModerationLogs_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_BookingModerationLogs_BookingId] ON [BookingModerationLogs] ([BookingId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250926164117_AddBookingModerationLogTable', N'9.0.0');

ALTER TABLE [BookingModerationLogs] ADD [ActorName] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250926203409_AddActroNameBookingModerationLogTable', N'9.0.0');

CREATE INDEX [IX_ReactiveEnrollmentMonthPayments_ReactiveEnrollmentId_ReactiveCourseMonthId] ON [ReactiveEnrollmentMonthPayments] ([ReactiveEnrollmentId], [ReactiveCourseMonthId]);

ALTER TABLE [Bookings] ADD [Price] decimal(18,2) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251010181406_somePropandTable', N'9.0.0');

COMMIT;
GO

