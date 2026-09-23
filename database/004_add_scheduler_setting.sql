START TRANSACTION;

CREATE TABLE `SystemSettings` (
    `Key` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ValueHash` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_SystemSettings` PRIMARY KEY (`Key`)
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260826042905_AddSchedulerSetting', '8.0.20');

COMMIT;

