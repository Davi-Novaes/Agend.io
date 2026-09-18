#!/bin/sh
# Mesma logica de infra/postgres/init/01-roles-and-database.sql (dev), mas
# le as senhas do ambiente do container em vez de hardcodar -- assim o
# arquivo commitado no repo nunca carrega segredo real de producao. Rodado
# uma unica vez pelo docker-entrypoint-initdb.d, na primeira subida com
# volume de dados vazio.
set -e

: "${AGENDIO_OWNER_PASSWORD:?AGENDIO_OWNER_PASSWORD precisa estar definida}"
: "${AGENDIO_APP_PASSWORD:?AGENDIO_APP_PASSWORD precisa estar definida}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
	CREATE ROLE agendio_owner WITH LOGIN PASSWORD '$AGENDIO_OWNER_PASSWORD' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
	CREATE ROLE agendio_app   WITH LOGIN PASSWORD '$AGENDIO_APP_PASSWORD'   NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

	CREATE DATABASE agendio OWNER agendio_owner;
EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname agendio <<-EOSQL
	CREATE EXTENSION IF NOT EXISTS btree_gist;
	CREATE EXTENSION IF NOT EXISTS vector;

	GRANT CONNECT ON DATABASE agendio TO agendio_app;
	GRANT USAGE ON SCHEMA public TO agendio_app;

	ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT USAGE ON SCHEMAS TO agendio_app;
	ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO agendio_app;
	ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT USAGE, SELECT ON SEQUENCES TO agendio_app;
EOSQL
