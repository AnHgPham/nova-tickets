START TRANSACTION;

ALTER TABLE `Seats` ADD `SeatAreaId` binary(16) NULL;

ALTER TABLE `Seats` ADD `TicketTypeId` binary(16) NULL;

CREATE TABLE `SeatAreas` (
    `Id` binary(16) NOT NULL,
    `VenueId` binary(16) NOT NULL,
    `Code` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `Color` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `SortOrder` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_SeatAreas` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SeatAreas_Venues_VenueId` FOREIGN KEY (`VenueId`) REFERENCES `Venues` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `TicketTypes` (
    `Id` binary(16) NOT NULL,
    `Code` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `Description` varchar(600) CHARACTER SET utf8mb4 NULL,
    `Color` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `BasePrice` decimal(18,2) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_TicketTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `PerformanceTicketTypes` (
    `PerformanceId` binary(16) NOT NULL,
    `TicketTypeId` binary(16) NOT NULL,
    `Price` decimal(18,2) NOT NULL,
    `Capacity` int NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_PerformanceTicketTypes` PRIMARY KEY (`PerformanceId`, `TicketTypeId`),
    CONSTRAINT `FK_PerformanceTicketTypes_Performances_PerformanceId` FOREIGN KEY (`PerformanceId`) REFERENCES `Performances` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_PerformanceTicketTypes_TicketTypes_TicketTypeId` FOREIGN KEY (`TicketTypeId`) REFERENCES `TicketTypes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_Seats_SeatAreaId` ON `Seats` (`SeatAreaId`);

CREATE INDEX `IX_Seats_TicketTypeId` ON `Seats` (`TicketTypeId`);

CREATE INDEX `IX_PerformanceTicketTypes_TicketTypeId` ON `PerformanceTicketTypes` (`TicketTypeId`);

CREATE UNIQUE INDEX `IX_SeatAreas_VenueId_Code` ON `SeatAreas` (`VenueId`, `Code`);

CREATE UNIQUE INDEX `IX_TicketTypes_Code` ON `TicketTypes` (`Code`);

ALTER TABLE `Seats` ADD CONSTRAINT `FK_Seats_SeatAreas_SeatAreaId` FOREIGN KEY (`SeatAreaId`) REFERENCES `SeatAreas` (`Id`) ON DELETE SET NULL;

ALTER TABLE `Seats` ADD CONSTRAINT `FK_Seats_TicketTypes_TicketTypeId` FOREIGN KEY (`TicketTypeId`) REFERENCES `TicketTypes` (`Id`) ON DELETE SET NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260826031934_AddCatalogEntities', '8.0.20');

COMMIT;

