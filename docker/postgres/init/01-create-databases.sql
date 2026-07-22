-- Auto-create the four ParcelTrack databases on first init only.
-- The postgres entrypoint runs this once, when the data volume is empty.

SELECT 'CREATE DATABASE parceltrack_shipment'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'parceltrack_shipment')\gexec

SELECT 'CREATE DATABASE parceltrack_notification'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'parceltrack_notification')\gexec

SELECT 'CREATE DATABASE parceltrack_tracking'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'parceltrack_tracking')\gexec

SELECT 'CREATE DATABASE parceltrack_keycloak'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'parceltrack_keycloak')\gexec
