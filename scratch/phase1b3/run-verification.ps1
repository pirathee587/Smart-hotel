$ErrorActionPreference = 'Stop'
$started = Get-Date
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
$role = "phase1b3_retry_runner_$stamp"
$hotelDb = "smarthotel_hotelops_phase1b3_retry_${stamp}_test"
$bookingDb = "smarthotel_booking_phase1b3_retry_${stamp}_test"
$password = -join ((1..48) | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) })
$container = 'smarthotel-postgres-test'
$hostName = 'host.docker.internal'
$port = '5433'
$hotelProject = Join-Path $PSScriptRoot '..\..\services\hotel-ops-service\SmartHotel.HotelOps\SmartHotel.HotelOps.Infrastructure\SmartHotel.HotelOps.Infrastructure.csproj'
$bookingProject = Join-Path $PSScriptRoot '..\..\services\booking-payments-service\SmartHotel.Booking\SmartHotel.Booking.Infrastructure\SmartHotel.Booking.Infrastructure.csproj'
$env:DOTNET_ROOT = Split-Path (Get-Command dotnet).Source

function Invoke-AdminSql([string]$sql) {
    $result = docker exec $container psql -U test_admin -d postgres -X -v ON_ERROR_STOP=1 -Atc $sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Admin SQL failed: $($result -join ' ')" }
    return $result
}

function Invoke-TestSql([string]$database, [string]$sql) {
    # Lossless SQL transport: UTF-8 -> base64 -> file inside the isolated container.
    # This avoids PowerShell/native argument parsing altering quoted identifiers.
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
    $remotePath = "/tmp/phase1b3a-$([Guid]::NewGuid().ToString('N')).sql"
    $writeResult = docker exec $container sh -c "echo '$encoded' | base64 -d > '$remotePath'" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Unable to stage SQL in isolated container: $($writeResult -join ' ')" }
    $result = docker exec -e "PGPASSWORD=$password" $container psql -h $hostName -p $port -U $role -d $database -X -v ON_ERROR_STOP=1 -At -f $remotePath 2>&1
    $psqlExitCode = $LASTEXITCODE
    docker exec $container rm -f $remotePath | Out-Null
    if ($psqlExitCode -ne 0) { throw "Test SQL failed for $database`: $($result -join ' ')" }
    return ($result -join "`n").Trim()
}

function Invoke-EfUpdate([string]$database, [string]$migration, [string]$project) {
    $connection = "Host=127.0.0.1;Port=$port;Database=$database;Username=$role;Password=$password;Include Error Detail=false"
    $result = dotnet ef database update $migration --project $project --no-build --connection $connection 2>&1
    if ($LASTEXITCODE -ne 0) { throw "EF migration update failed for $database to $migration`: $($result -join ' ')" }
    $connection = $null
}

function Assert-Equal([string]$label, $actual, $expected) {
    if ($actual -ne $expected) { throw "$label expected [$expected], got [$actual]" }
}

try {
    $preflight = Invoke-AdminSql "SELECT current_setting('port') || '|' || (SELECT rolsuper::text FROM pg_roles WHERE rolname='smarthotel_test_runner');"
    Assert-Equal 'preflight server port and existing runner superuser flag' $preflight '5432|false'

    Invoke-AdminSql "CREATE ROLE $role LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '$password';" | Out-Null
    Invoke-AdminSql "CREATE DATABASE $hotelDb OWNER $role;" | Out-Null
    Invoke-AdminSql "CREATE DATABASE $bookingDb OWNER $role;" | Out-Null

    $roleCheck = Invoke-TestSql $hotelDb "SELECT current_user || '|superuser=' || rolsuper::text || '|database=' || current_database() FROM pg_roles WHERE rolname=current_user;"
    Assert-Equal 'active role' $roleCheck "$role|superuser=false|database=$hotelDb"

    # HotelOps: predecessor, synthetic historical rows, target migration.
    Invoke-EfUpdate $hotelDb '20260918173028_InitialVersionedSchema' $hotelProject
    Assert-Equal 'HotelOps predecessor history' (Invoke-TestSql $hotelDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";') '20260918173028_InitialVersionedSchema'
    $hotelIdentifiers = Invoke-TestSql $hotelDb "SELECT table_schema||'|'||table_name FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Hotels','RoomTypes') ORDER BY table_name;"
    Assert-Equal 'HotelOps identifier discovery' $hotelIdentifiers "public|Hotels`npublic|RoomTypes"
    Invoke-TestSql $hotelDb @'
INSERT INTO "Hotels" ("Id","Name","Address","Phone","Email","TotalFloors","TotalRooms","CreatedAtUtc","UpdatedAtUtc")
VALUES ('10000000-0000-0000-0000-000000000001','SmartHotel','Synthetic Test Address','+94000000000','phase1b3@example.test',2,10,'2026-09-23T00:00:00Z',NULL);
INSERT INTO "RoomTypes" ("Id","HotelId","Name","Title","BedType","Capacity","RoomSizeSqFt","PricePerNight","CleaningFee","AmenitiesFee","LongDescription","Highlights","Amenities","CancellationPolicyText","IsPublished","IsActive","CreatedAtUtc","UpdatedAtUtc")
VALUES ('10000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001','Synthetic','Synthetic Room','King',2,300,12345.67,100.00,50.00,'test','test','test','test',true,true,'2026-09-23T00:00:00Z',NULL);
'@ | Out-Null
    Assert-Equal 'HotelOps pre-migration BaseCurrency absence' (Invoke-TestSql $hotelDb "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='Hotels' AND column_name='BaseCurrency';") '0'
    $hotelBefore = Invoke-TestSql $hotelDb "SELECT md5((to_jsonb(h))::text) FROM \"Hotels\" h WHERE \"Id\"='10000000-0000-0000-0000-000000000001';"
    $roomTypeBefore = Invoke-TestSql $hotelDb "SELECT md5((to_jsonb(r))::text) FROM \"RoomTypes\" r WHERE \"Id\"='10000000-0000-0000-0000-000000000002';"
    $hotelSchemaBefore = Invoke-TestSql $hotelDb "SELECT md5(string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable||':'||coalesce(column_default,''),',' ORDER BY table_name,ordinal_position)) FROM information_schema.columns WHERE table_schema='public' AND table_name NOT IN ('Hotels','__EFMigrationsHistory');"
    Invoke-EfUpdate $hotelDb '20260923144512_AddHotelBaseCurrency' $hotelProject
    Assert-Equal 'HotelOps BaseCurrency catalog' (Invoke-TestSql $hotelDb "SELECT data_type||'|'||character_maximum_length||'|'||is_nullable||'|'||column_default FROM information_schema.columns WHERE table_schema='public' AND table_name='Hotels' AND column_name='BaseCurrency';") "character varying|3|NO|'LKR'::character varying"
    Assert-Equal 'HotelOps historical row default' (Invoke-TestSql $hotelDb "SELECT \"BaseCurrency\" FROM \"Hotels\" WHERE \"Id\"='10000000-0000-0000-0000-000000000001';") 'LKR'
    Assert-Equal 'HotelOps hotel row unchanged except BaseCurrency' (Invoke-TestSql $hotelDb "SELECT md5((to_jsonb(h)-'BaseCurrency')::text) FROM \"Hotels\" h WHERE \"Id\"='10000000-0000-0000-0000-000000000001';") $hotelBefore
    Assert-Equal 'HotelOps RoomType unchanged' (Invoke-TestSql $hotelDb "SELECT md5((to_jsonb(r))::text) FROM \"RoomTypes\" r WHERE \"Id\"='10000000-0000-0000-0000-000000000002';") $roomTypeBefore
    Assert-Equal 'HotelOps unrelated schema unchanged' (Invoke-TestSql $hotelDb "SELECT md5(string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable||':'||coalesce(column_default,''),',' ORDER BY table_name,ordinal_position)) FROM information_schema.columns WHERE table_schema='public' AND table_name NOT IN ('Hotels','__EFMigrationsHistory');") $hotelSchemaBefore
    $hotelHistory = Invoke-TestSql $hotelDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
    Assert-Equal 'HotelOps migration history' $hotelHistory "20260918173028_InitialVersionedSchema`n20260923144512_AddHotelBaseCurrency"

    # Booking: predecessor, two bookings (one paid), one draft, then target migration.
    Invoke-EfUpdate $bookingDb '20260922180000_AddCheckoutInitiatedAtUtcAndBackfill' $bookingProject
    Assert-Equal 'Booking predecessor history' (Invoke-TestSql $bookingDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";') "20260918172955_InitialVersionedSchema`n20260922180000_AddCheckoutInitiatedAtUtcAndBackfill"
    $bookingIdentifiers = Invoke-TestSql $bookingDb "SELECT table_schema||'|'||table_name FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Bookings','BookingDrafts','Payments') ORDER BY table_name;"
    Assert-Equal 'Booking identifier discovery' $bookingIdentifiers "public|BookingDrafts`npublic|Bookings`npublic|Payments"
    Invoke-TestSql $bookingDb @'
INSERT INTO "Bookings" ("Id","BookingReference","CustomerId","CustomerLastName","CustomerEmail","RoomId","RoomNumber","RoomTypeId","CheckInDate","CheckOutDate","GuestCount","TotalAmount","Status","PaymentReference","PayHereOrderId","CreatedAtUtc","UpdatedAtUtc","CheckoutInitiatedAtUtc") VALUES
('20000000-0000-0000-0000-000000000001','PHASE1B3-NOPAY','21000000-0000-0000-0000-000000000001','Test','nopay@example.test','22000000-0000-0000-0000-000000000001','T01','23000000-0000-0000-0000-000000000001','2026-10-01','2026-10-03',2,1000.00,1,NULL,NULL,'2026-09-23T00:00:00Z',NULL,NULL),
('20000000-0000-0000-0000-000000000002','PHASE1B3-PAID','21000000-0000-0000-0000-000000000002','Test','paid@example.test','22000000-0000-0000-0000-000000000002','T02','23000000-0000-0000-0000-000000000002','2026-10-04','2026-10-06',2,2000.00,2,'SYNTHETIC-PAYMENT','SYNTHETIC-ORDER','2026-09-23T00:00:00Z',NULL,NULL);
INSERT INTO "BookingDrafts" ("Id","CustomerId","CustomerEmail","RoomId","RoomName","RoomTypeId","RatePlanId","RatePlanName","CheckInDate","CheckOutDate","GuestCount","RoomsCount","PricePerNight","Nights","TaxesAndFees","TotalAmount","IsUpgraded","OriginalRoomId","OriginalRoomName","SessionSuppressedUpsell","Status","CreatedAtUtc","UpdatedAtUtc")
VALUES ('20000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000003','draft@example.test','22000000-0000-0000-0000-000000000003','Synthetic Room','23000000-0000-0000-0000-000000000003','STANDARD','Standard','2026-10-07','2026-10-09',2,1,1500.00,2,300.00,3300.00,false,NULL,NULL,false,'Draft','2026-09-23T00:00:00Z',NULL);
INSERT INTO "Payments" ("Id","BookingId","Amount","Currency","Provider","PayHereOrderId","PayHerePaymentId","ProviderPaymentReference","ProviderCheckoutReference","RefundedAmount","Status","CapturedAt","RefundedAt","RefundAmount","RefundReason","FundsHeldAtUtc","ReleasedAtUtc","SettledAtUtc","CreatedAtUtc","UpdatedAtUtc")
VALUES ('20000000-0000-0000-0000-000000000004','20000000-0000-0000-0000-000000000002',2000.00,'LKR',0,'SYNTHETIC-ORDER',NULL,NULL,NULL,0.00,1,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-23T00:00:00Z',NULL);
'@ | Out-Null
    Assert-Equal 'Booking pre-migration columns absence' (Invoke-TestSql $bookingDb "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('Bookings','BookingDrafts') AND column_name='Currency';") '0'
    $bookingsBefore = Invoke-TestSql $bookingDb 'SELECT md5(string_agg(to_jsonb(b)::text,'','' ORDER BY "Id")) FROM "Bookings" b;'
    $draftBefore = Invoke-TestSql $bookingDb 'SELECT md5(string_agg(to_jsonb(d)::text,'','' ORDER BY "Id")) FROM "BookingDrafts" d;'
    $paymentBefore = Invoke-TestSql $bookingDb 'SELECT md5(string_agg(to_jsonb(p)::text,'','' ORDER BY "Id")) FROM "Payments" p;'
    $bookingSchemaBefore = Invoke-TestSql $bookingDb "SELECT md5(string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable||':'||coalesce(column_default,''),',' ORDER BY table_name,ordinal_position)) FROM information_schema.columns WHERE table_schema='public' AND table_name NOT IN ('Bookings','BookingDrafts','__EFMigrationsHistory');"
    Invoke-EfUpdate $bookingDb '20260923144605_AddBookingAndDraftCurrency' $bookingProject
    Assert-Equal 'Booking Currency catalogs' (Invoke-TestSql $bookingDb "SELECT string_agg(table_name||'|'||data_type||'|'||character_maximum_length||'|'||is_nullable||'|'||coalesce(column_default,'NULL'), E'\n' ORDER BY table_name) FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('Bookings','BookingDrafts') AND column_name='Currency';") "BookingDrafts|character varying|3|YES|NULL`nBookings|character varying|3|YES|NULL"
    Assert-Equal 'Historical Booking NULL preservation' (Invoke-TestSql $bookingDb 'SELECT count(*) FROM "Bookings" WHERE "Currency" IS NULL;') '2'
    Assert-Equal 'Historical Draft NULL preservation' (Invoke-TestSql $bookingDb 'SELECT count(*) FROM "BookingDrafts" WHERE "Currency" IS NULL;') '1'
    Assert-Equal 'Historical Booking rows unchanged' (Invoke-TestSql $bookingDb 'SELECT md5(string_agg((to_jsonb(b)-''Currency'')::text,'','' ORDER BY "Id")) FROM "Bookings" b;') $bookingsBefore
    Assert-Equal 'Historical Draft rows unchanged' (Invoke-TestSql $bookingDb 'SELECT md5(string_agg((to_jsonb(d)-''Currency'')::text,'','' ORDER BY "Id")) FROM "BookingDrafts" d;') $draftBefore
    Assert-Equal 'Payment rows unchanged' (Invoke-TestSql $bookingDb 'SELECT md5(string_agg(to_jsonb(p)::text,'','' ORDER BY "Id")) FROM "Payments" p;') $paymentBefore
    Assert-Equal 'Booking unrelated schema unchanged' (Invoke-TestSql $bookingDb "SELECT md5(string_agg(table_name||':'||column_name||':'||data_type||':'||is_nullable||':'||coalesce(column_default,''),',' ORDER BY table_name,ordinal_position)) FROM information_schema.columns WHERE table_schema='public' AND table_name NOT IN ('Bookings','BookingDrafts','__EFMigrationsHistory');") $bookingSchemaBefore
    $bookingHistory = Invoke-TestSql $bookingDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
    Assert-Equal 'Booking migration history' $bookingHistory "20260918172955_InitialVersionedSchema`n20260922180000_AddCheckoutInitiatedAtUtcAndBackfill`n20260923144605_AddBookingAndDraftCurrency"

    # Roll back only the target migration, verify removal, then forward again.
    Invoke-EfUpdate $hotelDb '20260918173028_InitialVersionedSchema' $hotelProject
    Assert-Equal 'HotelOps rollback removed column' (Invoke-TestSql $hotelDb "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='Hotels' AND column_name='BaseCurrency';") '0'
    Assert-Equal 'HotelOps rollback history' (Invoke-TestSql $hotelDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";') '20260918173028_InitialVersionedSchema'
    Invoke-EfUpdate $hotelDb '20260923144512_AddHotelBaseCurrency' $hotelProject
    Assert-Equal 'HotelOps re-forward schema/data' (Invoke-TestSql $hotelDb "SELECT \"BaseCurrency\" FROM \"Hotels\" WHERE \"Id\"='10000000-0000-0000-0000-000000000001';") 'LKR'

    Invoke-EfUpdate $bookingDb '20260922180000_AddCheckoutInitiatedAtUtcAndBackfill' $bookingProject
    Assert-Equal 'Booking rollback removed columns' (Invoke-TestSql $bookingDb "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('Bookings','BookingDrafts') AND column_name='Currency';") '0'
    Assert-Equal 'Booking rollback history' (Invoke-TestSql $bookingDb 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";') "20260918172955_InitialVersionedSchema`n20260922180000_AddCheckoutInitiatedAtUtcAndBackfill"
    Invoke-EfUpdate $bookingDb '20260923144605_AddBookingAndDraftCurrency' $bookingProject
    Assert-Equal 'Booking re-forward historical NULLs' (Invoke-TestSql $bookingDb 'SELECT (SELECT count(*) FROM "Bookings" WHERE "Currency" IS NULL)::text || ''|'' || (SELECT count(*) FROM "BookingDrafts" WHERE "Currency" IS NULL)::text;') '2|1'

    # Narrow persistence check for the model's new-write shape; no payment gateway/network call.
    Invoke-TestSql $bookingDb @'
INSERT INTO "BookingDrafts" ("Id","CustomerId","CustomerEmail","RoomId","RoomName","RoomTypeId","RatePlanId","RatePlanName","CheckInDate","CheckOutDate","GuestCount","RoomsCount","PricePerNight","Nights","TaxesAndFees","TotalAmount","IsUpgraded","OriginalRoomId","OriginalRoomName","SessionSuppressedUpsell","Status","CreatedAtUtc","UpdatedAtUtc","Currency")
VALUES ('30000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','new@example.test','32000000-0000-0000-0000-000000000001','New Write Room','33000000-0000-0000-0000-000000000001','STANDARD','Standard','2026-11-01','2026-11-03',2,1,2500.00,2,500.00,5500.00,false,NULL,NULL,false,'Completed','2026-09-23T01:00:00Z',NULL,'LKR');
INSERT INTO "Bookings" ("Id","BookingReference","CustomerId","CustomerLastName","CustomerEmail","RoomId","RoomNumber","RoomTypeId","CheckInDate","CheckOutDate","GuestCount","TotalAmount","Status","PaymentReference","PayHereOrderId","CreatedAtUtc","UpdatedAtUtc","CheckoutInitiatedAtUtc","Currency")
VALUES ('30000000-0000-0000-0000-000000000002','PHASE1B3-NEW','31000000-0000-0000-0000-000000000001','Test','new@example.test','32000000-0000-0000-0000-000000000001','N01','33000000-0000-0000-0000-000000000001','2026-11-01','2026-11-03',2,5500.00,2,'SYNTHETIC-NEW','SYNTHETIC-NEW','2026-09-23T01:00:00Z',NULL,NULL,'LKR');
INSERT INTO "Payments" ("Id","BookingId","Amount","Currency","Provider","PayHereOrderId","PayHerePaymentId","ProviderPaymentReference","ProviderCheckoutReference","RefundedAmount","Status","CapturedAt","RefundedAt","RefundAmount","RefundReason","FundsHeldAtUtc","ReleasedAtUtc","SettledAtUtc","CreatedAtUtc","UpdatedAtUtc")
VALUES ('30000000-0000-0000-0000-000000000003','30000000-0000-0000-0000-000000000002',5500.00,'LKR',0,'SYNTHETIC-NEW',NULL,NULL,NULL,0.00,1,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-23T01:00:00Z',NULL);
'@ | Out-Null
    Assert-Equal 'new-write persisted currencies' (Invoke-TestSql $bookingDb "SELECT (SELECT \"Currency\" FROM \"BookingDrafts\" WHERE \"Id\"='30000000-0000-0000-0000-000000000001') || '|' || (SELECT \"Currency\" FROM \"Bookings\" WHERE \"Id\"='30000000-0000-0000-0000-000000000002') || '|' || (SELECT \"Currency\" FROM \"Payments\" WHERE \"Id\"='30000000-0000-0000-0000-000000000003');") 'LKR|LKR|LKR'

    [pscustomobject]@{
        Status = 'PASS'
        HotelDatabase = $hotelDb
        BookingDatabase = $bookingDb
        Role = $role
        RoleSuperuser = $false
        HostPort = 5433
        HotelHistory = ($hotelHistory -replace "`n", ', ')
        BookingHistory = ($bookingHistory -replace "`n", ', ')
        HotelIdentifiers = ($hotelIdentifiers -replace "`n", ', ')
        BookingIdentifiers = ($bookingIdentifiers -replace "`n", ', ')
        DurationSeconds = [math]::Round(((Get-Date) - $started).TotalSeconds, 2)
    } | Format-List
}
finally {
    $env:ConnectionStrings__DefaultConnection = $null
    $env:TEST_DATABASE_CONNECTION = $null
    $env:ALLOW_TEST_DB_MODIFICATIONS = $null
    $env:DOTNET_ROOT = $null
    $password = $null
}
