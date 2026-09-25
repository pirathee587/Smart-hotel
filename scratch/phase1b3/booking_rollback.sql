START TRANSACTION;

ALTER TABLE "Bookings" DROP COLUMN "Currency";

ALTER TABLE "BookingDrafts" DROP COLUMN "Currency";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260923144605_AddBookingAndDraftCurrency';

COMMIT;

