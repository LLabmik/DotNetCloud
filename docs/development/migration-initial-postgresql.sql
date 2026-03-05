CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'core') THEN
            CREATE SCHEMA core;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetRoles" (
        "Id" uuid NOT NULL,
        "Description" character varying(500),
        "IsSystemRole" boolean NOT NULL DEFAULT FALSE,
        "Name" character varying(256),
        "NormalizedName" character varying(256),
        "ConcurrencyStamp" text,
        CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetUsers" (
        "Id" uuid NOT NULL,
        "DisplayName" character varying(200) NOT NULL,
        "AvatarUrl" character varying(500),
        "Locale" character varying(10) NOT NULL DEFAULT 'en-US',
        "Timezone" character varying(50) NOT NULL DEFAULT 'UTC',
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "LastLoginAt" timestamp with time zone,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "UserName" character varying(256),
        "NormalizedUserName" character varying(256),
        "Email" character varying(256),
        "NormalizedEmail" character varying(256),
        "EmailConfirmed" boolean NOT NULL,
        "PasswordHash" text,
        "SecurityStamp" text,
        "ConcurrencyStamp" text,
        "PhoneNumber" text,
        "PhoneNumberConfirmed" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "LockoutEnabled" boolean NOT NULL,
        "AccessFailedCount" integer NOT NULL,
        CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "InstalledModules" (
        "ModuleId" character varying(200) NOT NULL,
        "Version" character varying(50) NOT NULL,
        "Status" character varying(50) NOT NULL,
        "InstalledAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_InstalledModules" PRIMARY KEY ("ModuleId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE core.open_iddict_applications (
        "Id" uuid NOT NULL,
        "ApplicationType" character varying(50),
        "ClientId" character varying(100),
        "ClientSecret" text,
        "ClientType" character varying(50),
        "ConcurrencyToken" character varying(50),
        "ConsentType" character varying(50),
        "DisplayName" text,
        "DisplayNames" text,
        "JsonWebKeySet" text,
        "Permissions" text,
        "PostLogoutRedirectUris" text,
        "Properties" text,
        "RedirectUris" text,
        "Requirements" text,
        "Settings" text,
        CONSTRAINT "PK_open_iddict_applications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE core.open_iddict_scopes (
        "Id" uuid NOT NULL,
        "ConcurrencyToken" character varying(50),
        "Description" text,
        "Descriptions" text,
        "DisplayName" text,
        "DisplayNames" text,
        "Name" character varying(200),
        "Properties" text,
        "Resources" text,
        CONSTRAINT "PK_open_iddict_scopes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "Organizations" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Organizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "Permissions" (
        "Id" uuid NOT NULL,
        "Code" character varying(255) NOT NULL,
        "DisplayName" character varying(200) NOT NULL,
        "Description" character varying(1000),
        CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "Roles" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "IsSystemRole" boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "SystemSettings" (
        module character varying(100) NOT NULL,
        key character varying(200) NOT NULL,
        value character varying(10000) NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        description character varying(500),
        CONSTRAINT "PK_SystemSettings" PRIMARY KEY (module, key)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetRoleClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "RoleId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetUserClaims" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "UserId" uuid NOT NULL,
        "ClaimType" text,
        "ClaimValue" text,
        CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetUserLogins" (
        "LoginProvider" text NOT NULL,
        "ProviderKey" text NOT NULL,
        "ProviderDisplayName" text,
        "UserId" uuid NOT NULL,
        CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
        CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetUserRoles" (
        "UserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
        CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "AspNetUserTokens" (
        "UserId" uuid NOT NULL,
        "LoginProvider" text NOT NULL,
        "Name" text NOT NULL,
        "Value" text,
        CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
        CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "FidoCredentials" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CredentialId" bytea NOT NULL,
        "PublicKey" bytea NOT NULL,
        "SignatureCounter" bigint NOT NULL DEFAULT 0,
        "DeviceName" character varying(200),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_FidoCredentials" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_FidoCredentials_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "UserBackupCodes" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CodeHash" character varying(128) NOT NULL,
        "IsUsed" boolean NOT NULL DEFAULT FALSE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        CONSTRAINT "PK_UserBackupCodes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserBackupCodes_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "UserDevices" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "DeviceType" character varying(50) NOT NULL,
        "PushToken" character varying(500),
        "LastSeenAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        CONSTRAINT "PK_UserDevices" PRIMARY KEY ("Id"),
        CONSTRAINT fk_user_devices_user_id FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "UserSettings" (
        "Id" uuid NOT NULL,
        user_id uuid NOT NULL,
        key character varying(200) NOT NULL,
        value character varying(10000) NOT NULL,
        module character varying(100) NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        description character varying(500),
        is_encrypted boolean NOT NULL DEFAULT FALSE,
        CONSTRAINT "PK_UserSettings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_user_settings_user_id" FOREIGN KEY (user_id) REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "ModuleCapabilityGrants" (
        "Id" uuid NOT NULL,
        "ModuleId" character varying(200) NOT NULL,
        "CapabilityName" character varying(200) NOT NULL,
        "GrantedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "GrantedByUserId" uuid,
        CONSTRAINT "PK_ModuleCapabilityGrants" PRIMARY KEY ("Id"),
        CONSTRAINT fk_module_capability_grants_granted_by_user_id FOREIGN KEY ("GrantedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT fk_module_capability_grants_module_id FOREIGN KEY ("ModuleId") REFERENCES "InstalledModules" ("ModuleId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE core.open_iddict_authorizations (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid,
        "ConcurrencyToken" character varying(50),
        "CreationDate" timestamp with time zone,
        "Properties" text,
        "Scopes" text,
        "Status" character varying(50),
        "Subject" character varying(400),
        "Type" character varying(50),
        CONSTRAINT "PK_open_iddict_authorizations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_open_iddict_authorizations_open_iddict_applications_Applica~" FOREIGN KEY ("ApplicationId") REFERENCES core.open_iddict_applications ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "Groups" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Groups" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Groups_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "OrganizationMembers" (
        "OrganizationId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RoleIds" jsonb NOT NULL DEFAULT '[]',
        "JoinedAt" timestamp with time zone NOT NULL,
        "InvitedByUserId" uuid,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_OrganizationMembers" PRIMARY KEY ("OrganizationId", "UserId"),
        CONSTRAINT "FK_OrganizationMembers_AspNetUsers_InvitedByUserId" FOREIGN KEY ("InvitedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_OrganizationMembers_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrganizationMembers_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "OrganizationSettings" (
        "Id" uuid NOT NULL,
        organization_id uuid NOT NULL,
        key character varying(200) NOT NULL,
        value character varying(10000) NOT NULL,
        module character varying(100) NOT NULL,
        updated_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        description character varying(500),
        "OrganizationId1" uuid,
        CONSTRAINT "PK_OrganizationSettings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OrganizationSettings_Organizations_OrganizationId1" FOREIGN KEY ("OrganizationId1") REFERENCES "Organizations" ("Id"),
        CONSTRAINT "FK_organization_settings_organization_id" FOREIGN KEY (organization_id) REFERENCES "Organizations" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "Teams" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Description" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsDeleted" boolean NOT NULL DEFAULT FALSE,
        "DeletedAt" timestamp with time zone,
        CONSTRAINT "PK_Teams" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Teams_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "RolePermissions" (
        "RoleId" uuid NOT NULL,
        "PermissionId" uuid NOT NULL,
        CONSTRAINT "PK_role_permissions" PRIMARY KEY ("RoleId", "PermissionId"),
        CONSTRAINT "FK_role_permissions_permission_id" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_role_permissions_role_id" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE core.open_iddict_tokens (
        "Id" uuid NOT NULL,
        "ApplicationId" uuid,
        "AuthorizationId" uuid,
        "ConcurrencyToken" character varying(50),
        "CreationDate" timestamp with time zone,
        "ExpirationDate" timestamp with time zone,
        "Payload" text,
        "Properties" text,
        "RedemptionDate" timestamp with time zone,
        "ReferenceId" character varying(100),
        "Status" character varying(50),
        "Subject" character varying(400),
        "Type" character varying(150),
        CONSTRAINT "PK_open_iddict_tokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_open_iddict_tokens_open_iddict_applications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES core.open_iddict_applications ("Id"),
        CONSTRAINT "FK_open_iddict_tokens_open_iddict_authorizations_Authorization~" FOREIGN KEY ("AuthorizationId") REFERENCES core.open_iddict_authorizations ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "GroupMembers" (
        "GroupId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "AddedAt" timestamp with time zone NOT NULL,
        "AddedByUserId" uuid,
        CONSTRAINT "PK_GroupMembers" PRIMARY KEY ("GroupId", "UserId"),
        CONSTRAINT "FK_GroupMembers_AspNetUsers_AddedByUserId" FOREIGN KEY ("AddedByUserId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_GroupMembers_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GroupMembers_Groups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "Groups" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE TABLE "TeamMembers" (
        "TeamId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RoleIds" jsonb NOT NULL DEFAULT '[]',
        "JoinedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TeamMembers" PRIMARY KEY ("TeamId", "UserId"),
        CONSTRAINT "FK_TeamMembers_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TeamMembers_Teams_TeamId" FOREIGN KEY ("TeamId") REFERENCES "Teams" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationRoles_IsSystemRole" ON "AspNetRoles" ("IsSystemRole");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationRoles_Name" ON "AspNetRoles" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationUsers_DisplayName" ON "AspNetUsers" ("DisplayName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationUsers_Email" ON "AspNetUsers" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationUsers_IsActive" ON "AspNetUsers" ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_ApplicationUsers_LastLoginAt" ON "AspNetUsers" ("LastLoginAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_FidoCredentials_CredentialId" ON "FidoCredentials" ("CredentialId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_FidoCredentials_UserId" ON "FidoCredentials" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_group_members_added_at" ON "GroupMembers" ("AddedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_group_members_added_by" ON "GroupMembers" ("AddedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_group_members_user_id" ON "GroupMembers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_groups_created_at" ON "Groups" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_groups_is_deleted" ON "Groups" ("IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_groups_org_name" ON "Groups" ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_installed_modules_installed_at ON "InstalledModules" ("InstalledAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_installed_modules_status ON "InstalledModules" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_module_capability_grants_capability_name ON "ModuleCapabilityGrants" ("CapabilityName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_module_capability_grants_granted_by_user_id ON "ModuleCapabilityGrants" ("GrantedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_module_capability_grants_module_id ON "ModuleCapabilityGrants" ("ModuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX uq_module_capability_grants_module_id_capability_name ON "ModuleCapabilityGrants" ("ModuleId", "CapabilityName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_open_iddict_applications_ClientId" ON core.open_iddict_applications ("ClientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_open_iddict_authorizations_ApplicationId_Status_Subject_Type" ON core.open_iddict_authorizations ("ApplicationId", "Status", "Subject", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_open_iddict_scopes_Name" ON core.open_iddict_scopes ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_open_iddict_tokens_ApplicationId_Status_Subject_Type" ON core.open_iddict_tokens ("ApplicationId", "Status", "Subject", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_open_iddict_tokens_AuthorizationId" ON core.open_iddict_tokens ("AuthorizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_open_iddict_tokens_ReferenceId" ON core.open_iddict_tokens ("ReferenceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_org_members_invited_by" ON "OrganizationMembers" ("InvitedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_org_members_is_active" ON "OrganizationMembers" ("IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_org_members_joined_at" ON "OrganizationMembers" ("JoinedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_org_members_user_id" ON "OrganizationMembers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_organizations_created_at" ON "Organizations" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_organizations_is_deleted" ON "Organizations" ("IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_organizations_name" ON "Organizations" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_organization_settings_module" ON "OrganizationSettings" (module);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_organization_settings_organization_id" ON "OrganizationSettings" (organization_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_organization_settings_unique" ON "OrganizationSettings" (organization_id, module, key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_organization_settings_updated_at" ON "OrganizationSettings" (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_OrganizationSettings_OrganizationId1" ON "OrganizationSettings" ("OrganizationId1");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_permissions_code" ON "Permissions" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_role_permissions_permission_id" ON "RolePermissions" ("PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_role_permissions_role_id" ON "RolePermissions" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_roles_is_system_role" ON "Roles" ("IsSystemRole");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_roles_name" ON "Roles" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_system_settings_module" ON "SystemSettings" (module);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_system_settings_updated_at" ON "SystemSettings" (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_team_members_joined_at" ON "TeamMembers" ("JoinedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_team_members_user_id" ON "TeamMembers" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_teams_created_at" ON "Teams" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_teams_is_deleted" ON "Teams" ("IsDeleted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_teams_org_name" ON "Teams" ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_UserBackupCodes_UserId" ON "UserBackupCodes" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_UserBackupCodes_UserId_IsUsed" ON "UserBackupCodes" ("UserId", "IsUsed");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_user_devices_last_seen_at ON "UserDevices" ("LastSeenAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_user_devices_user_id ON "UserDevices" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX ix_user_devices_user_id_device_type ON "UserDevices" ("UserId", "DeviceType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_user_settings_is_encrypted" ON "UserSettings" (is_encrypted);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_user_settings_module" ON "UserSettings" (module);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_user_settings_unique" ON "UserSettings" (user_id, module, key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_user_settings_updated_at" ON "UserSettings" (updated_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    CREATE INDEX "IX_user_settings_user_id" ON "UserSettings" (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102502_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260305102502_InitialCreate', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_authorizations DROP CONSTRAINT "FK_open_iddict_authorizations_open_iddict_applications_Applica~";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_tokens DROP CONSTRAINT "FK_open_iddict_tokens_open_iddict_applications_ApplicationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_tokens DROP CONSTRAINT "FK_open_iddict_tokens_open_iddict_authorizations_Authorization~";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    DROP INDEX "UserNameIndex";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    DROP INDEX "RoleNameIndex";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_tokens DROP CONSTRAINT "PK_open_iddict_tokens";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    DROP INDEX core."IX_open_iddict_tokens_ReferenceId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_scopes DROP CONSTRAINT "PK_open_iddict_scopes";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    DROP INDEX core."IX_open_iddict_scopes_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_authorizations DROP CONSTRAINT "PK_open_iddict_authorizations";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_applications DROP CONSTRAINT "PK_open_iddict_applications";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    DROP INDEX core."IX_open_iddict_applications_ClientId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_tokens RENAME TO "OpenIddictTokens";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_scopes RENAME TO "OpenIddictScopes";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_authorizations RENAME TO "OpenIddictAuthorizations";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core.open_iddict_applications RENAME TO "OpenIddictApplications";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER INDEX core."IX_open_iddict_tokens_AuthorizationId" RENAME TO "IX_OpenIddictTokens_AuthorizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER INDEX core."IX_open_iddict_tokens_ApplicationId_Status_Subject_Type" RENAME TO "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER INDEX core."IX_open_iddict_authorizations_ApplicationId_Status_Subject_Type" RENAME TO "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN value TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN user_id TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN updated_at TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN module TYPE nvarchar(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN key TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN is_encrypted TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN description TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserSettings" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "PushToken" TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "LastSeenAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "DeviceType" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserDevices" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "UsedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "IsUsed" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "CodeHash" TYPE nvarchar(128);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "UserBackupCodes" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "OrganizationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "IsDeleted" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "Description" TYPE nvarchar(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "DeletedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Teams" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "TeamMembers" ALTER COLUMN "RoleIds" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "TeamMembers" ALTER COLUMN "JoinedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "TeamMembers" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "TeamMembers" ALTER COLUMN "TeamId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "SystemSettings" ALTER COLUMN value TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "SystemSettings" ALTER COLUMN updated_at TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "SystemSettings" ALTER COLUMN description TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "SystemSettings" ALTER COLUMN key TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "SystemSettings" ALTER COLUMN module TYPE nvarchar(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Roles" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Roles" ALTER COLUMN "IsSystemRole" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Roles" ALTER COLUMN "Description" TYPE nvarchar(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Roles" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "RolePermissions" ALTER COLUMN "PermissionId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "RolePermissions" ALTER COLUMN "RoleId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Permissions" ALTER COLUMN "DisplayName" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Permissions" ALTER COLUMN "Description" TYPE nvarchar(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Permissions" ALTER COLUMN "Code" TYPE nvarchar(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Permissions" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN value TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN updated_at TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN organization_id TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN module TYPE nvarchar(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN key TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN description TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN "OrganizationId1" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationSettings" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "IsDeleted" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "Description" TYPE nvarchar(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "DeletedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "RoleIds" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "JoinedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "IsActive" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "InvitedByUserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "OrganizationMembers" ALTER COLUMN "OrganizationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "ModuleCapabilityGrants" ALTER COLUMN "ModuleId" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "ModuleCapabilityGrants" ALTER COLUMN "GrantedByUserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "ModuleCapabilityGrants" ALTER COLUMN "GrantedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "ModuleCapabilityGrants" ALTER COLUMN "CapabilityName" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "ModuleCapabilityGrants" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "InstalledModules" ALTER COLUMN "Version" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "InstalledModules" ALTER COLUMN "UpdatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "InstalledModules" ALTER COLUMN "Status" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "InstalledModules" ALTER COLUMN "InstalledAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "InstalledModules" ALTER COLUMN "ModuleId" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "OrganizationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "IsDeleted" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "Description" TYPE nvarchar(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "DeletedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "Groups" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "GroupMembers" ALTER COLUMN "AddedByUserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "GroupMembers" ALTER COLUMN "AddedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "GroupMembers" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "GroupMembers" ALTER COLUMN "GroupId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "PublicKey" TYPE varbinary(1024);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "DeviceName" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "CredentialId" TYPE varbinary(1024);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "FidoCredentials" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserTokens" ALTER COLUMN "Value" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserTokens" ALTER COLUMN "Name" TYPE nvarchar(450);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserTokens" ALTER COLUMN "LoginProvider" TYPE nvarchar(450);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserTokens" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "UserName" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "TwoFactorEnabled" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "Timezone" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "SecurityStamp" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "PhoneNumberConfirmed" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "PhoneNumber" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "PasswordHash" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "NormalizedUserName" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "NormalizedEmail" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "LockoutEnd" TYPE datetimeoffset;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "LockoutEnabled" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "Locale" TYPE nvarchar(10);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "LastLoginAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "IsActive" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "EmailConfirmed" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "Email" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "DisplayName" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "CreatedAt" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "ConcurrencyStamp" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "AvatarUrl" TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "AccessFailedCount" TYPE int;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUsers" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserRoles" ALTER COLUMN "RoleId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserRoles" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserLogins" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserLogins" ALTER COLUMN "ProviderDisplayName" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserLogins" ALTER COLUMN "ProviderKey" TYPE nvarchar(450);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserLogins" ALTER COLUMN "LoginProvider" TYPE nvarchar(450);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserClaims" ALTER COLUMN "UserId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserClaims" ALTER COLUMN "ClaimValue" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserClaims" ALTER COLUMN "ClaimType" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetUserClaims" ALTER COLUMN "Id" TYPE int;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "NormalizedName" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "Name" TYPE nvarchar(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "IsSystemRole" TYPE bit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "Description" TYPE nvarchar(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "ConcurrencyStamp" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoles" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoleClaims" ALTER COLUMN "RoleId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoleClaims" ALTER COLUMN "ClaimValue" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoleClaims" ALTER COLUMN "ClaimType" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE "AspNetRoleClaims" ALTER COLUMN "Id" TYPE int;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Type" TYPE nvarchar(150);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Subject" TYPE nvarchar(400);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Status" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "ReferenceId" TYPE nvarchar(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "RedemptionDate" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Properties" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Payload" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "ExpirationDate" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "CreationDate" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "ConcurrencyToken" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "AuthorizationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "ApplicationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Resources" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Properties" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Name" TYPE nvarchar(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "DisplayNames" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "DisplayName" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Descriptions" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Description" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "ConcurrencyToken" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Type" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Subject" TYPE nvarchar(400);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Status" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Scopes" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Properties" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "CreationDate" TYPE datetime2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "ConcurrencyToken" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "ApplicationId" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "Settings" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "Requirements" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "RedirectUris" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "Properties" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "PostLogoutRedirectUris" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "Permissions" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "JsonWebKeySet" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "DisplayNames" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "DisplayName" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ConsentType" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ConcurrencyToken" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ClientType" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ClientSecret" TYPE nvarchar(max);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ClientId" TYPE nvarchar(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "ApplicationType" TYPE nvarchar(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ALTER COLUMN "Id" TYPE uniqueidentifier;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ADD CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictScopes" ADD CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ADD CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictApplications" ADD CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName") WHERE [NormalizedUserName] IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName") WHERE [NormalizedName] IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictTokens_ReferenceId" ON core."OpenIddictTokens" ("ReferenceId") WHERE [ReferenceId] IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictScopes_Name" ON core."OpenIddictScopes" ("Name") WHERE [Name] IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    CREATE UNIQUE INDEX "IX_OpenIddictApplications_ClientId" ON core."OpenIddictApplications" ("ClientId") WHERE [ClientId] IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictAuthorizations" ADD CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES core."OpenIddictApplications" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ADD CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES core."OpenIddictApplications" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    ALTER TABLE core."OpenIddictTokens" ADD CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES core."OpenIddictAuthorizations" ("Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260305102714_InitialCreate_SqlServer') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260305102714_InitialCreate_SqlServer', '10.0.3');
    END IF;
END $EF$;
COMMIT;

