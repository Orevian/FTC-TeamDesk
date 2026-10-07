-- FTC TeamDesk schema v1. Column names match the EF Core model exactly (see TeamDeskDbContext).
CREATE TABLE "MemberStatuses" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Key" TEXT NOT NULL,
  "Name" TEXT NULL,
  "Color" TEXT NOT NULL,
  "IsSystem" INTEGER NOT NULL,
  "SortOrder" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "IX_MemberStatuses_Key" ON "MemberStatuses" ("Key");

CREATE TABLE "Areas" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Key" TEXT NOT NULL,
  "Name" TEXT NULL,
  "IsSystem" INTEGER NOT NULL,
  "SortOrder" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "IX_Areas_Key" ON "Areas" ("Key");

CREATE TABLE "Skills" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Name" TEXT NOT NULL COLLATE NOCASE
);
CREATE UNIQUE INDEX "IX_Skills_Name" ON "Skills" ("Name");

CREATE TABLE "Members" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "FullName" TEXT NOT NULL,
  "Email" TEXT NULL,
  "Phone" TEXT NULL,
  "AvatarPath" TEXT NULL,
  "StatusId" TEXT NOT NULL REFERENCES "MemberStatuses" ("Id") ON DELETE RESTRICT,
  "JoinDate" TEXT NOT NULL,
  "Responsibilities" TEXT NULL,
  "Notes" TEXT NULL,
  "CreatedAt" TEXT NOT NULL,
  "UpdatedAt" TEXT NOT NULL
);
CREATE INDEX "IX_Members_StatusId" ON "Members" ("StatusId");
CREATE INDEX "IX_Members_FullName" ON "Members" ("FullName");

CREATE TABLE "MemberSkills" (
  "MemberId" TEXT NOT NULL REFERENCES "Members" ("Id") ON DELETE CASCADE,
  "SkillId" TEXT NOT NULL REFERENCES "Skills" ("Id") ON DELETE CASCADE,
  PRIMARY KEY ("MemberId", "SkillId")
);
CREATE INDEX "IX_MemberSkills_SkillId" ON "MemberSkills" ("SkillId");

CREATE TABLE "MemberAreas" (
  "MemberId" TEXT NOT NULL REFERENCES "Members" ("Id") ON DELETE CASCADE,
  "AreaId" TEXT NOT NULL REFERENCES "Areas" ("Id") ON DELETE CASCADE,
  PRIMARY KEY ("MemberId", "AreaId")
);
CREATE INDEX "IX_MemberAreas_AreaId" ON "MemberAreas" ("AreaId");

CREATE TABLE "MemberCustomFields" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "MemberId" TEXT NOT NULL REFERENCES "Members" ("Id") ON DELETE CASCADE,
  "Name" TEXT NOT NULL,
  "Value" TEXT NOT NULL
);
CREATE INDEX "IX_MemberCustomFields_MemberId" ON "MemberCustomFields" ("MemberId");

CREATE TABLE "PerformanceRecords" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "MemberId" TEXT NOT NULL REFERENCES "Members" ("Id") ON DELETE CASCADE,
  "AreaId" TEXT NULL REFERENCES "Areas" ("Id") ON DELETE SET NULL,
  "RecordedOn" TEXT NOT NULL,
  "Score" INTEGER NOT NULL,
  "Note" TEXT NULL
);
CREATE INDEX "IX_PerformanceRecords_MemberId" ON "PerformanceRecords" ("MemberId", "RecordedOn");

CREATE TABLE "Applications" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "ApplicantName" TEXT NOT NULL,
  "Email" TEXT NULL,
  "SubmittedAt" TEXT NOT NULL,
  "Status" TEXT NOT NULL,
  "Skills" TEXT NULL,
  "Experience" TEXT NULL,
  "Notes" TEXT NULL,
  "ExternalKey" TEXT NULL,
  "CreatedAt" TEXT NOT NULL
);
CREATE INDEX "IX_Applications_SubmittedAt" ON "Applications" ("SubmittedAt");
CREATE INDEX "IX_Applications_Status" ON "Applications" ("Status");
CREATE INDEX "IX_Applications_ExternalKey" ON "Applications" ("ExternalKey");

CREATE TABLE "ApplicationAnswers" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "ApplicationId" TEXT NOT NULL REFERENCES "Applications" ("Id") ON DELETE CASCADE,
  "Question" TEXT NOT NULL,
  "Answer" TEXT NOT NULL,
  "SortOrder" INTEGER NOT NULL
);
CREATE INDEX "IX_ApplicationAnswers_ApplicationId" ON "ApplicationAnswers" ("ApplicationId");

CREATE TABLE "Budgets" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Name" TEXT NOT NULL,
  "TotalAmount" TEXT NOT NULL,
  "Currency" TEXT NOT NULL,
  "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "BudgetCategories" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Key" TEXT NOT NULL,
  "Name" TEXT NULL,
  "IsSystem" INTEGER NOT NULL,
  "PlannedAmount" TEXT NOT NULL,
  "SortOrder" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "IX_BudgetCategories_Key" ON "BudgetCategories" ("Key");

CREATE TABLE "Purchases" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Product" TEXT NOT NULL,
  "CategoryId" TEXT NOT NULL REFERENCES "BudgetCategories" ("Id") ON DELETE RESTRICT,
  "UnitPrice" TEXT NOT NULL,
  "Quantity" INTEGER NOT NULL,
  "PurchaseDate" TEXT NOT NULL,
  "Status" TEXT NOT NULL,
  "Seller" TEXT NULL,
  "Notes" TEXT NULL,
  "CreatedAt" TEXT NOT NULL
);
CREATE INDEX "IX_Purchases_CategoryId" ON "Purchases" ("CategoryId");
CREATE INDEX "IX_Purchases_PurchaseDate" ON "Purchases" ("PurchaseDate");

CREATE TABLE "AiSettings" (
  "Id" INTEGER NOT NULL PRIMARY KEY,
  "Provider" TEXT NOT NULL,
  "Model" TEXT NOT NULL,
  "Temperature" REAL NOT NULL,
  "MaxTokens" INTEGER NOT NULL,
  "BaseUrl" TEXT NULL,
  "IsEnabled" INTEGER NOT NULL
);

CREATE TABLE "AiPermissions" (
  "Key" TEXT NOT NULL PRIMARY KEY,
  "IsEnabled" INTEGER NOT NULL
);

CREATE TABLE "PasswordVault" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "EncryptedPayload" BLOB NOT NULL,
  "CreatedAt" TEXT NOT NULL,
  "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "PasswordVaultMeta" (
  "Id" INTEGER NOT NULL PRIMARY KEY,
  "Salt" BLOB NOT NULL,
  "Iterations" INTEGER NOT NULL,
  "Verifier" BLOB NOT NULL,
  "FailedAttempts" INTEGER NOT NULL,
  "LockedUntil" TEXT NULL
);

CREATE TABLE "ActivityLog" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Timestamp" TEXT NOT NULL,
  "ActionType" TEXT NOT NULL,
  "Description" TEXT NOT NULL,
  "Actor" TEXT NOT NULL
);
CREATE INDEX "IX_ActivityLog_Timestamp" ON "ActivityLog" ("Timestamp");

CREATE TABLE "AppSettings" (
  "Key" TEXT NOT NULL PRIMARY KEY,
  "Value" TEXT NULL
);

CREATE TABLE "Tasks" (
  "Id" TEXT NOT NULL PRIMARY KEY,
  "Title" TEXT NOT NULL,
  "Notes" TEXT NULL,
  "DueDate" TEXT NULL,
  "IsCompleted" INTEGER NOT NULL,
  "CreatedAt" TEXT NOT NULL
);
CREATE INDEX "IX_Tasks_DueDate" ON "Tasks" ("DueDate");
