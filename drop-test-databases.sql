-- ==============================================================================
-- Script: drop-test-databases.sql
-- Description: Drops ONLY legacy/temporary test databases created during QA runs.
-- TARGET DATABASES:
--   - AyurvedaApiDb
--   - JwtAuthDemoDb
--   - AyurvedaDb_ProdRehearsal
--   - AyurvedaDb_ProdPass2
--   - AyurvedaDb_PrePushProd
--
-- CAUTION: DO NOT RUN THIS SCRIPT AUTOMATICALLY.
-- Run manually in SQL Server Management Studio (SSMS) or via sqlcmd only when
-- you explicitly intend to clean up local test databases.
-- ==============================================================================

USE [master];
GO

-- 1. Drop AyurvedaApiDb
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'AyurvedaApiDb')
BEGIN
    PRINT 'Dropping database [AyurvedaApiDb]...';
    ALTER DATABASE [AyurvedaApiDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AyurvedaApiDb];
    PRINT 'Database [AyurvedaApiDb] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [AyurvedaApiDb] does not exist. Skipping.';
END
GO

-- 2. Drop JwtAuthDemoDb
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'JwtAuthDemoDb')
BEGIN
    PRINT 'Dropping database [JwtAuthDemoDb]...';
    ALTER DATABASE [JwtAuthDemoDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [JwtAuthDemoDb];
    PRINT 'Database [JwtAuthDemoDb] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [JwtAuthDemoDb] does not exist. Skipping.';
END
GO

-- 3. Drop AyurvedaDb_ProdRehearsal
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'AyurvedaDb_ProdRehearsal')
BEGIN
    PRINT 'Dropping database [AyurvedaDb_ProdRehearsal]...';
    ALTER DATABASE [AyurvedaDb_ProdRehearsal] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AyurvedaDb_ProdRehearsal];
    PRINT 'Database [AyurvedaDb_ProdRehearsal] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [AyurvedaDb_ProdRehearsal] does not exist. Skipping.';
END
GO

-- 4. Drop AyurvedaDb_ProdPass2
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'AyurvedaDb_ProdPass2')
BEGIN
    PRINT 'Dropping database [AyurvedaDb_ProdPass2]...';
    ALTER DATABASE [AyurvedaDb_ProdPass2] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AyurvedaDb_ProdPass2];
    PRINT 'Database [AyurvedaDb_ProdPass2] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [AyurvedaDb_ProdPass2] does not exist. Skipping.';
END
GO

-- 5. Drop AyurvedaDb_PrePushProd
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'AyurvedaDb_PrePushProd')
BEGIN
    PRINT 'Dropping database [AyurvedaDb_PrePushProd]...';
    ALTER DATABASE [AyurvedaDb_PrePushProd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AyurvedaDb_PrePushProd];
    PRINT 'Database [AyurvedaDb_PrePushProd] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [AyurvedaDb_PrePushProd] does not exist. Skipping.';
END
GO

PRINT 'Cleanup script completed. Only the 5 designated test databases were targeted.';
GO
