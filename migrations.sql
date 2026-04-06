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
