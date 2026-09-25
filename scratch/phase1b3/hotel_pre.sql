CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE "Hotels" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Address" character varying(500) NOT NULL,
    "Phone" character varying(50) NOT NULL,
    "Email" character varying(150) NOT NULL,
    "TotalFloors" integer NOT NULL,
    "TotalRooms" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Hotels" PRIMARY KEY ("Id")
);

CREATE TABLE "HousekeepingReadinessRecords" (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "RoomId" uuid NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "HousekeeperId" uuid NOT NULL,
    "ManagerId" uuid,
    "Status" integer NOT NULL,
    "StartedAtUtc" timestamp with time zone NOT NULL,
    "InspectedAtUtc" timestamp with time zone,
    "InspectionNotes" character varying(1000),
    "ReadinessGate" character varying(120) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_HousekeepingReadinessRecords" PRIMARY KEY ("Id")
);

CREATE TABLE "MaintenanceRestrictions" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "RoomId" uuid NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "ReportedBy" uuid NOT NULL,
    "ClearedBy" uuid,
    "Issue" character varying(2000) NOT NULL,
    "Severity" character varying(20) NOT NULL,
    "SafetyHazard" boolean NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ReportedAtUtc" timestamp with time zone NOT NULL,
    "ClearedAtUtc" timestamp with time zone,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_MaintenanceRestrictions" PRIMARY KEY ("Id")
);

CREATE TABLE "OperationalEventReceipts" (
    "EventId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "RoomId" uuid NOT NULL,
    "EventType" character varying(80) NOT NULL,
    "OccurredAtUtc" timestamp with time zone NOT NULL,
    "ProcessedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OperationalEventReceipts" PRIMARY KEY ("EventId")
);

CREATE TABLE "OutboxMessages" (
    "Id" uuid NOT NULL,
    "OccurredOnUtc" timestamp with time zone NOT NULL,
    "Type" character varying(100) NOT NULL,
    "Content" text NOT NULL,
    "ProcessedOnUtc" timestamp with time zone,
    "Error" text,
    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
);

CREATE TABLE "Departments" (
    "Id" uuid NOT NULL,
    "HotelId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "ManagerId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Departments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Departments_Hotels_HotelId" FOREIGN KEY ("HotelId") REFERENCES "Hotels" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RoomTypes" (
    "Id" uuid NOT NULL,
    "HotelId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Title" character varying(200) NOT NULL,
    "BedType" character varying(100) NOT NULL,
    "Capacity" integer NOT NULL,
    "RoomSizeSqFt" integer NOT NULL,
    "PricePerNight" numeric(18,2) NOT NULL,
    "CleaningFee" numeric(18,2) NOT NULL,
    "AmenitiesFee" numeric(18,2) NOT NULL,
    "LongDescription" text NOT NULL,
    "Highlights" text NOT NULL,
    "Amenities" text NOT NULL,
    "CancellationPolicyText" text NOT NULL,
    "IsPublished" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_RoomTypes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RoomTypes_Hotels_HotelId" FOREIGN KEY ("HotelId") REFERENCES "Hotels" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Rooms" (
    "Id" uuid NOT NULL,
    "HotelId" uuid NOT NULL,
    "RoomTypeId" uuid NOT NULL,
    "RoomNumber" character varying(50) NOT NULL,
    "Floor" integer NOT NULL,
    "Status" integer NOT NULL,
    "OutOfOrderReason" character varying(500),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Rooms" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Rooms_Hotels_HotelId" FOREIGN KEY ("HotelId") REFERENCES "Hotels" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Rooms_RoomTypes_RoomTypeId" FOREIGN KEY ("RoomTypeId") REFERENCES "RoomTypes" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "RoomTypeImages" (
    "Id" uuid NOT NULL,
    "RoomTypeId" uuid NOT NULL,
    "ImageUrl" character varying(1000) NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_RoomTypeImages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RoomTypeImages_RoomTypes_RoomTypeId" FOREIGN KEY ("RoomTypeId") REFERENCES "RoomTypes" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Departments_HotelId" ON "Departments" ("HotelId");

CREATE INDEX "IX_HousekeepingReadinessRecords_RoomId_Status" ON "HousekeepingReadinessRecords" ("RoomId", "Status");

CREATE UNIQUE INDEX "IX_HousekeepingReadinessRecords_TaskId" ON "HousekeepingReadinessRecords" ("TaskId");

CREATE INDEX "IX_MaintenanceRestrictions_RoomId_Status" ON "MaintenanceRestrictions" ("RoomId", "Status");

CREATE UNIQUE INDEX "IX_MaintenanceRestrictions_WorkOrderId" ON "MaintenanceRestrictions" ("WorkOrderId");

CREATE INDEX "IX_OperationalEventReceipts_ProcessedAtUtc" ON "OperationalEventReceipts" ("ProcessedAtUtc");

CREATE INDEX "IX_OperationalEventReceipts_TaskId_EventType" ON "OperationalEventReceipts" ("TaskId", "EventType");

CREATE INDEX "IX_OutboxMessages_ProcessedOnUtc" ON "OutboxMessages" ("ProcessedOnUtc");

CREATE UNIQUE INDEX "IX_Rooms_HotelId_RoomNumber" ON "Rooms" ("HotelId", "RoomNumber");

CREATE INDEX "IX_Rooms_RoomTypeId" ON "Rooms" ("RoomTypeId");

CREATE INDEX "IX_Rooms_Status" ON "Rooms" ("Status");

CREATE INDEX "IX_RoomTypeImages_RoomTypeId" ON "RoomTypeImages" ("RoomTypeId");

CREATE INDEX "IX_RoomTypes_HotelId" ON "RoomTypes" ("HotelId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260918173028_InitialVersionedSchema', '8.0.8');

COMMIT;

