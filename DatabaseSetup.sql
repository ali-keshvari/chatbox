-- ===================================================================
-- اسکریپت ایجاد دیتابیس و جداول ChatBoxDb برای SQL Server Management Studio (SSMS)
-- نکته: برنامه با استفاده از Entity Framework Core و EnsureCreated به صورت خودکار
-- در زمان اجرای اولیه این جداول را در صورت عدم وجود می‌سازد.
-- اما در صورت تمایل می‌توانید این اسکریپت را مستقیماً در SSMS اجرا نمایید.
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'ChatBoxDb')
BEGIN
    CREATE DATABASE [ChatBoxDb];
END
GO

USE [ChatBoxDb];
GO

-- 1. جدول گفتگوها (Conversations)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Conversations')
BEGIN
    CREATE TABLE [dbo].[Conversations] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Title] NVARCHAR(250) NOT NULL,
        [ModelName] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE NONCLUSTERED INDEX [IX_Conversations_UpdatedAt] ON [dbo].[Conversations]([UpdatedAt] DESC);
END
GO

-- 2. جدول پیام‌های چت (ChatMessages)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'ChatMessages')
BEGIN
    CREATE TABLE [dbo].[ChatMessages] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ConversationId] INT NOT NULL,
        [Role] NVARCHAR(30) NOT NULL, -- 'user', 'assistant', 'system'
        [Content] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_ChatMessages_Conversations] FOREIGN KEY ([ConversationId]) 
            REFERENCES [dbo].[Conversations]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_ChatMessages_ConversationId] ON [dbo].[ChatMessages]([ConversationId]);
    CREATE NONCLUSTERED INDEX [IX_ChatMessages_CreatedAt] ON [dbo].[ChatMessages]([CreatedAt]);
END
GO

-- 3. جدول فایل‌های بارگذاری شده و متن استخراج شده (FileAttachments)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'FileAttachments')
BEGIN
    CREATE TABLE [dbo].[FileAttachments] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ChatMessageId] INT NOT NULL,
        [FileName] NVARCHAR(260) NOT NULL,
        [ContentType] NVARCHAR(100) NULL,
        [FileExtension] NVARCHAR(20) NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [ExtractedText] NVARCHAR(MAX) NOT NULL, -- متن کامل استخراج شده از PDF، Word، Text جهت تحلیل مدل
        [UploadedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_FileAttachments_ChatMessages] FOREIGN KEY ([ChatMessageId]) 
            REFERENCES [dbo].[ChatMessages]([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_FileAttachments_ChatMessageId] ON [dbo].[FileAttachments]([ChatMessageId]);
END
GO
