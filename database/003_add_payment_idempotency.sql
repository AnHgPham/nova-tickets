START TRANSACTION;

ALTER TABLE `Payments` DROP INDEX `IX_Payments_BookingId`;

ALTER TABLE `Payments` ADD `IdempotencyKey` varchar(100) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';

CREATE UNIQUE INDEX `IX_Payments_BookingId_IdempotencyKey` ON `Payments` (`BookingId`, `IdempotencyKey`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260826034932_AddPaymentIdempotency', '8.0.20');

COMMIT;

