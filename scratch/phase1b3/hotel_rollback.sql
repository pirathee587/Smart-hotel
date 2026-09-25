START TRANSACTION;

ALTER TABLE "Hotels" DROP COLUMN "BaseCurrency";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260923144512_AddHotelBaseCurrency';

COMMIT;

