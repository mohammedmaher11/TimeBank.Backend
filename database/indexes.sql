/*
    TimeBank
    Index script (idempotent).

    The EF Core migrations create these indexes as part of the schema. This
    script exists so the indexing decisions can be reviewed and re-applied on
    an existing database without dropping data. Every index below is explained
    in docs/indexes.md.

    Run with:
        sqlcmd -S "localhost\SQLEXPRESS" -C -E -d TimeBankDb -i database\indexes.sql
*/
SET NOCOUNT ON;
GO

/* -------------------------------------------------------------------------
   Members
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Members_Email' AND object_id = OBJECT_ID(N'dbo.Members'))
    CREATE UNIQUE INDEX [IX_Members_Email] ON [dbo].[Members] ([Email]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Members_District' AND object_id = OBJECT_ID(N'dbo.Members'))
    CREATE INDEX [IX_Members_District] ON [dbo].[Members] ([District]);
GO

/* -------------------------------------------------------------------------
   Categories / Skills / MemberSkills
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Categories_Name' AND object_id = OBJECT_ID(N'dbo.Categories'))
    CREATE UNIQUE INDEX [IX_Categories_Name] ON [dbo].[Categories] ([Name]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Skills_CategoryId_Name' AND object_id = OBJECT_ID(N'dbo.Skills'))
    CREATE UNIQUE INDEX [IX_Skills_CategoryId_Name] ON [dbo].[Skills] ([CategoryId], [Name]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MemberSkills_SkillId' AND object_id = OBJECT_ID(N'dbo.MemberSkills'))
    CREATE INDEX [IX_MemberSkills_SkillId] ON [dbo].[MemberSkills] ([SkillId]);
GO

/* -------------------------------------------------------------------------
   HelpRequests
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_Status_CategoryId_District' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_Status_CategoryId_District]
        ON [dbo].[HelpRequests] ([Status], [CategoryId], [District]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_CreatedAt' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_CreatedAt] ON [dbo].[HelpRequests] ([CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_RequesterId' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_RequesterId] ON [dbo].[HelpRequests] ([RequesterId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_AcceptedHelperId' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_AcceptedHelperId] ON [dbo].[HelpRequests] ([AcceptedHelperId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_AcceptedOfferId' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_AcceptedOfferId] ON [dbo].[HelpRequests] ([AcceptedOfferId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HelpRequests_CategoryId' AND object_id = OBJECT_ID(N'dbo.HelpRequests'))
    CREATE INDEX [IX_HelpRequests_CategoryId] ON [dbo].[HelpRequests] ([CategoryId]);
GO

/* -------------------------------------------------------------------------
   Offers
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Offers_HelpRequestId_HelperId' AND object_id = OBJECT_ID(N'dbo.Offers'))
    CREATE UNIQUE INDEX [IX_Offers_HelpRequestId_HelperId]
        ON [dbo].[Offers] ([HelpRequestId], [HelperId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Offers_HelpRequestId_Status' AND object_id = OBJECT_ID(N'dbo.Offers'))
    CREATE INDEX [IX_Offers_HelpRequestId_Status] ON [dbo].[Offers] ([HelpRequestId], [Status]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Offers_HelperId' AND object_id = OBJECT_ID(N'dbo.Offers'))
    CREATE INDEX [IX_Offers_HelperId] ON [dbo].[Offers] ([HelperId]);
GO

/* -------------------------------------------------------------------------
   LedgerEntries
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LedgerEntries_MemberId_CreatedAt' AND object_id = OBJECT_ID(N'dbo.LedgerEntries'))
    CREATE INDEX [IX_LedgerEntries_MemberId_CreatedAt]
        ON [dbo].[LedgerEntries] ([MemberId], [CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LedgerEntries_MemberId_Type' AND object_id = OBJECT_ID(N'dbo.LedgerEntries'))
    CREATE INDEX [IX_LedgerEntries_MemberId_Type]
        ON [dbo].[LedgerEntries] ([MemberId], [Type]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LedgerEntries_ReferenceId' AND object_id = OBJECT_ID(N'dbo.LedgerEntries'))
    CREATE INDEX [IX_LedgerEntries_ReferenceId] ON [dbo].[LedgerEntries] ([ReferenceId]);
GO

/* -------------------------------------------------------------------------
   Reviews / Badges / AuditLogs
   ------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_HelpRequestId_ReviewerId' AND object_id = OBJECT_ID(N'dbo.Reviews'))
    CREATE UNIQUE INDEX [IX_Reviews_HelpRequestId_ReviewerId]
        ON [dbo].[Reviews] ([HelpRequestId], [ReviewerId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_RevieweeId' AND object_id = OBJECT_ID(N'dbo.Reviews'))
    CREATE INDEX [IX_Reviews_RevieweeId] ON [dbo].[Reviews] ([RevieweeId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Reviews_ReviewerId' AND object_id = OBJECT_ID(N'dbo.Reviews'))
    CREATE INDEX [IX_Reviews_ReviewerId] ON [dbo].[Reviews] ([ReviewerId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Badges_Code' AND object_id = OBJECT_ID(N'dbo.Badges'))
    CREATE UNIQUE INDEX [IX_Badges_Code] ON [dbo].[Badges] ([Code]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MemberBadges_BadgeId' AND object_id = OBJECT_ID(N'dbo.MemberBadges'))
    CREATE INDEX [IX_MemberBadges_BadgeId] ON [dbo].[MemberBadges] ([BadgeId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_CreatedAt' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [dbo].[AuditLogs] ([CreatedAt]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Actor' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    CREATE INDEX [IX_AuditLogs_Actor] ON [dbo].[AuditLogs] ([Actor]);
GO
