-- Two Factor Authentication (TOTP) support for Users
-- Adds columns to store the TOTP secret, enrollment status and recovery codes.

IF COL_LENGTH('dbo.Users', 'TwoFactorEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TwoFactorEnabled BIT NOT NULL CONSTRAINT DF_Users_TwoFactorEnabled DEFAULT 0;
END
GO

IF COL_LENGTH('dbo.Users', 'TwoFactorSecretKey') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TwoFactorSecretKey NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.Users', 'TwoFactorRecoveryCodes') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD TwoFactorRecoveryCodes NVARCHAR(500) NULL;
END
GO