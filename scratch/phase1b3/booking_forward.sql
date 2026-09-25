START TRANSACTION;

ALTER TABLE "Bookings" ADD "Currency" character varying(3);

ALTER TABLE "BookingDrafts" ADD "Currency" character varying(3);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260923144605_AddBookingAndDraftCurrency', '8.0.8');

COMMIT;

