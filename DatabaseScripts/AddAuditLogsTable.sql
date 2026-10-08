-- =============================================================
-- AuditLogs table  (Data change audit trail)
-- Run this script once on the application database.
-- =============================================================

IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        AuditDate   DATETIME         NOT NULL CONSTRAINT DF_AuditLogs_AuditDate DEFAULT GETDATE(),
        UserId      INT              NULL,
        UserName    NVARCHAR(200)    NULL,
        UserType    INT              NULL,
        ReferenceId INT              NULL,
        Module      NVARCHAR(100)    NULL,
        Action      NVARCHAR(50)     NULL,
        EntityName  NVARCHAR(100)    NULL,
        EntityId    NVARCHAR(200)    NULL,
        Description NVARCHAR(1000)   NULL,
        OldValues   NVARCHAR(MAX)    NULL,
        NewValues   NVARCHAR(MAX)    NULL,
        IpAddress   NVARCHAR(100)    NULL,
        UserAgent   NVARCHAR(500)    NULL,
        PageName    NVARCHAR(200)    NULL,
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id)
    );

    PRINT 'Table dbo.AuditLogs created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.AuditLogs already exists.';
END
GO

-- =============================================================
-- Indexes
-- =============================================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_AuditDate' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_AuditDate ON dbo.AuditLogs (AuditDate DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Module_Action' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_Module_Action ON dbo.AuditLogs (Module, Action);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_UserId' AND object_id = OBJECT_ID('dbo.AuditLogs'))
    CREATE INDEX IX_AuditLogs_UserId ON dbo.AuditLogs (UserId);
GO

-- =============================================================
-- Optional: retention cleanup (run monthly from SQL Agent)
-- Keep last 12 months of audit history.
-- =============================================================
-- DELETE FROM dbo.AuditLogs WHERE AuditDate < DATEADD(MONTH, -12, GETDATE());
-- GO
