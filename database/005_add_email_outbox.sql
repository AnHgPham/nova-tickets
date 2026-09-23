-- NOVA Tickets plain SQL migration 005
-- Adds the offline email outbox used by the demo confirmation flow.
-- Apply only after 001_initial_novatickets_plain.sql through 004_add_scheduler_setting.sql.

CREATE TABLE IF NOT EXISTS `EmailOutbox` (
  `Id` BINARY(16) NOT NULL,
  `BookingId` BINARY(16) NOT NULL,
  `MessageType` VARCHAR(60) NOT NULL,
  `ToEmail` VARCHAR(320) NOT NULL,
  `Subject` VARCHAR(200) NOT NULL,
  `HtmlBody` LONGTEXT NOT NULL,
  `TextBody` LONGTEXT NOT NULL,
  `Provider` VARCHAR(40) NOT NULL,
  `Status` VARCHAR(255) NOT NULL,
  `AttemptCount` INT NOT NULL DEFAULT 0,
  `LastError` VARCHAR(1000) NULL,
  `CreatedAtUtc` DATETIME(6) NOT NULL,
  `UpdatedAtUtc` DATETIME(6) NOT NULL,
  `SentAtUtc` DATETIME(6) NULL,
  CONSTRAINT `PK_EmailOutbox` PRIMARY KEY (`Id`),
  CONSTRAINT `FK_EmailOutbox_Bookings_BookingId`
    FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE,
  UNIQUE KEY `IX_EmailOutbox_BookingId_MessageType` (`BookingId`, `MessageType`),
  KEY `IX_EmailOutbox_Status_CreatedAtUtc` (`Status`, `CreatedAtUtc`)
) CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
