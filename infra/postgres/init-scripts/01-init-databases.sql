-- Initialize multiple databases on first container startup
SELECT 'CREATE DATABASE smarthotel_hotelops_db'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'smarthotel_hotelops_db')\gexec

SELECT 'CREATE DATABASE smarthotel_bookings'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'smarthotel_bookings')\gexec

SELECT 'CREATE DATABASE smarthotel_fieldops'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'smarthotel_fieldops')\gexec

SELECT 'CREATE DATABASE smarthotel_notifications'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'smarthotel_notifications')\gexec
