/* AI Resume Analyzer – SQL Server schema
   The app creates this database automatically on first run (EF Core).
   This script is provided for reference / manual setup. */

IF DB_ID('ResumeAnalyzerDb') IS NULL
    CREATE DATABASE ResumeAnalyzerDb;
GO
USE ResumeAnalyzerDb;
GO

CREATE TABLE ResumeAnalyses (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    FileName        NVARCHAR(260) NOT NULL,
    FileType        NVARCHAR(10)  NOT NULL,      -- PDF | DOCX
    FileSizeBytes   BIGINT        NOT NULL,
    ExtractedText   NVARCHAR(MAX) NOT NULL,      -- text extracted from the resume
    JobDescription  NVARCHAR(MAX) NULL,          -- optional JD used for matching
    CandidateName   NVARCHAR(150) NOT NULL,
    OverallScore    INT           NOT NULL,      -- 0-100
    MatchScore      INT           NULL,          -- 0-100 (only when JD provided)
    ResultJson      NVARCHAR(MAX) NOT NULL,      -- full AI analysis (skills, experience, education, ...)
    AiProvider      NVARCHAR(100) NOT NULL,
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_ResumeAnalyses_CreatedAt ON ResumeAnalyses(CreatedAt);
CREATE INDEX IX_ResumeAnalyses_CandidateName ON ResumeAnalyses(CandidateName);
GO
