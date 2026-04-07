CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- Migration: AddInvites
CREATE TABLE IF NOT EXISTS "Invites" (
    "Id"            uuid            NOT NULL,
    "Token"         varchar(32)     NOT NULL,
    "Description"   varchar(200),
    "ExpiresAt"     timestamptz     NOT NULL,
    "CreatedAt"     timestamptz     NOT NULL,
    "UsedAt"        timestamptz,
    "UsedByUserId"  uuid            REFERENCES "Users"("Id") ON DELETE RESTRICT,
    CONSTRAINT "PK_Invites" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Invites_Token" ON "Invites"("Token");

-- Migration: AddTeamSoftDelete
ALTER TABLE "CS2Teams" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
ALTER TABLE "CS2Teams" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamptz;

-- Migration: AddAuditLogs
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id"            uuid                        NOT NULL,
    "ActorId"       uuid                        REFERENCES "Users"("Id") ON DELETE SET NULL,
    "ActorUsername" character varying(100)      NOT NULL,
    "Action"        character varying(100)      NOT NULL,
    "ResourceType"  character varying(50),
    "ResourceId"    character varying(100),
    "HttpMethod"    character varying(10)       NOT NULL,
    "Route"         character varying(200)      NOT NULL,
    "StatusCode"    integer                     NOT NULL,
    "IpAddress"     character varying(45),
    "OccurredAt"    timestamp with time zone    NOT NULL,
    "Details"       character varying(1000),
    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
);
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Action"     ON "AuditLogs"("Action");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_ActorId"    ON "AuditLogs"("ActorId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_OccurredAt" ON "AuditLogs"("OccurredAt");
