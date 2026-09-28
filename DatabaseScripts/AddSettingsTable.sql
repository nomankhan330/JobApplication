IF OBJECT_ID('dbo.Settings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Settings
    (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SettingKey    NVARCHAR(100)  NOT NULL,
        SettingValue  NVARCHAR(1000) NULL,
        IsActive      BIT            NULL,
        CreatedBy     INT            NULL,
        CreatedOn     DATETIME       NULL,
        ModifiedBy    INT            NULL,
        ModifiedOn    DATETIME       NULL,
        CONSTRAINT UQ_Settings_SettingKey UNIQUE (SettingKey)
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE SettingKey = 'EnableInvoiceQRCode')
BEGIN
    INSERT INTO dbo.Settings (SettingKey, SettingValue, IsActive, CreatedOn)
    VALUES ('EnableInvoiceQRCode', 'False', 1, GETDATE());
END