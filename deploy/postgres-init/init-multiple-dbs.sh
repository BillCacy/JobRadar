#!/usr/bin/env bash
# Runs once, the first time the postgres container starts with an empty data volume.
# The official postgres image only creates one database (POSTGRES_DB) out of the box; this
# creates one additional database per JobRadar service so each keeps its own schema, matching
# the "database per microservice" pattern described in the README.
set -e

for db in usersdb aggregatordb matchingdb notificationsdb; do
  echo "Creating database '$db' owned by '$POSTGRES_USER'..."
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT 'CREATE DATABASE $db OWNER $POSTGRES_USER'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
done
