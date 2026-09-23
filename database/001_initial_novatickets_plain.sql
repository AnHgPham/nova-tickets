CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AppUsers` (
    `Id` binary(16) NOT NULL,
    `Email` varchar(320) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `FullName` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `Phone` varchar(30) CHARACTER SET utf8mb4 NULL,
    `Role` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_AppUsers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` binary(16) NULL,
    `Action` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `EntityType` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` varchar(80) CHARACTER SET utf8mb4 NULL,
    `DataJson` varchar(4000) CHARACTER SET utf8mb4 NULL,
    `IpAddress` varchar(64) CHARACTER SET utf8mb4 NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Events` (
    `Id` binary(16) NOT NULL,
    `Title` varchar(180) CHARACTER SET utf8mb4 NOT NULL,
    `Slug` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Artist` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(3000) CHARACTER SET utf8mb4 NOT NULL,
    `HeroImageUrl` varchar(800) CHARACTER SET utf8mb4 NOT NULL,
    `MinPrice` decimal(18,2) NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Events` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Venues` (
    `Id` binary(16) NOT NULL,
    `Name` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
    `City` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `Address` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(1200) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Venues` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Performances` (
    `Id` binary(16) NOT NULL,
    `EventId` binary(16) NOT NULL,
    `VenueId` binary(16) NOT NULL,
    `StartsAtUtc` datetime(6) NOT NULL,
    `DoorsOpenAtUtc` datetime(6) NOT NULL,
    `SalesStartUtc` datetime(6) NOT NULL,
    `SalesEndUtc` datetime(6) NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Performances` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Performances_Events_EventId` FOREIGN KEY (`EventId`) REFERENCES `Events` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Performances_Venues_VenueId` FOREIGN KEY (`VenueId`) REFERENCES `Venues` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Seats` (
    `Id` binary(16) NOT NULL,
    `VenueId` binary(16) NOT NULL,
    `Section` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `RowLabel` varchar(12) CHARACTER SET utf8mb4 NOT NULL,
    `Number` int NOT NULL,
    `PositionX` int NOT NULL,
    `PositionY` int NOT NULL,
    `PriceTier` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_Seats` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Seats_Venues_VenueId` FOREIGN KEY (`VenueId`) REFERENCES `Venues` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Bookings` (
    `Id` binary(16) NOT NULL,
    `BookingCode` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `UserId` binary(16) NOT NULL,
    `PerformanceId` binary(16) NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TotalAmount` decimal(18,2) NOT NULL,
    `IdempotencyKey` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `CustomerName` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `CustomerEmail` varchar(320) CHARACTER SET utf8mb4 NOT NULL,
    `CustomerPhone` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `ExpiresAtUtc` datetime(6) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Bookings` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Bookings_AppUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AppUsers` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Bookings_Performances_PerformanceId` FOREIGN KEY (`PerformanceId`) REFERENCES `Performances` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `SeatInventory` (
    `PerformanceId` binary(16) NOT NULL,
    `SeatId` binary(16) NOT NULL,
    `Price` decimal(18,2) NOT NULL,
    `Status` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `HoldToken` varchar(80) CHARACTER SET utf8mb4 NULL,
    `HeldByUserId` binary(16) NULL,
    `HoldExpiresAtUtc` datetime(6) NULL,
    `BookingId` binary(16) NULL,
    `Version` bigint NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_SeatInventory` PRIMARY KEY (`PerformanceId`, `SeatId`),
    CONSTRAINT `FK_SeatInventory_Performances_PerformanceId` FOREIGN KEY (`PerformanceId`) REFERENCES `Performances` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_SeatInventory_Seats_SeatId` FOREIGN KEY (`SeatId`) REFERENCES `Seats` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `Payments` (
    `Id` binary(16) NOT NULL,
    `BookingId` binary(16) NOT NULL,
    `Provider` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `Reference` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Amount` decimal(18,2) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CompletedAtUtc` datetime(6) NULL,
    CONSTRAINT `PK_Payments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Payments_Bookings_BookingId` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Tickets` (
    `Id` binary(16) NOT NULL,
    `BookingId` binary(16) NOT NULL,
    `PerformanceId` binary(16) NOT NULL,
    `SeatId` binary(16) NOT NULL,
    `TicketCode` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `Price` decimal(18,2) NOT NULL,
    `IssuedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Tickets` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Tickets_Bookings_BookingId` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_Tickets_Seats_SeatId` FOREIGN KEY (`SeatId`) REFERENCES `Seats` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_AppUsers_Email` ON `AppUsers` (`Email`);

CREATE UNIQUE INDEX `IX_Bookings_BookingCode` ON `Bookings` (`BookingCode`);

CREATE INDEX `IX_Bookings_PerformanceId` ON `Bookings` (`PerformanceId`);

CREATE UNIQUE INDEX `IX_Bookings_UserId_IdempotencyKey` ON `Bookings` (`UserId`, `IdempotencyKey`);

CREATE UNIQUE INDEX `IX_Events_Slug` ON `Events` (`Slug`);

CREATE INDEX `IX_Payments_BookingId` ON `Payments` (`BookingId`);

CREATE UNIQUE INDEX `IX_Payments_Reference` ON `Payments` (`Reference`);

CREATE INDEX `IX_Performances_EventId` ON `Performances` (`EventId`);

CREATE INDEX `IX_Performances_VenueId` ON `Performances` (`VenueId`);

CREATE INDEX `IX_SeatInventory_HoldToken` ON `SeatInventory` (`HoldToken`);

CREATE INDEX `IX_SeatInventory_PerformanceId_Status` ON `SeatInventory` (`PerformanceId`, `Status`);

CREATE INDEX `IX_SeatInventory_SeatId` ON `SeatInventory` (`SeatId`);

CREATE UNIQUE INDEX `IX_Seats_VenueId_Section_RowLabel_Number` ON `Seats` (`VenueId`, `Section`, `RowLabel`, `Number`);

CREATE INDEX `IX_Tickets_BookingId` ON `Tickets` (`BookingId`);

CREATE UNIQUE INDEX `IX_Tickets_PerformanceId_SeatId` ON `Tickets` (`PerformanceId`, `SeatId`);

CREATE INDEX `IX_Tickets_SeatId` ON `Tickets` (`SeatId`);

CREATE UNIQUE INDEX `IX_Tickets_TicketCode` ON `Tickets` (`TicketCode`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260826031400_InitialNovaTickets', '8.0.20');

COMMIT;

