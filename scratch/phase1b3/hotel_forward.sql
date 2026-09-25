START TRANSACTION;

ALTER TABLE "Hotels" ADD "BaseCurrency" character varying(3) NOT NULL DEFAULT 'LKR';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260923144512_AddHotelBaseCurrency', '8.0.8');

COMMIT;

