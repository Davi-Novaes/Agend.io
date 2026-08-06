-- Executado UMA VEZ pelo docker-entrypoint-initdb.d na primeira subida do
-- container (volume de dados vazio), como o superusuario padrao da imagem.
--
-- Duas roles, dois propositos:
--   agendio_owner -> dona do banco/schemas/tabelas; roda migrations (DDL).
--                    Por padrao, PostgreSQL deixa o DONO de uma tabela IGNORAR
--                    Row Level Security — entao esta role NUNCA deve ser usada
--                    pela aplicacao em runtime, so pela ferramenta de migration.
--   agendio_app   -> usada pela API em runtime. Sem privilegio de DDL, sem
--                    BYPASSRLS (o padrao ja e NOBYPASSRLS; deixado explicito).
--                    E ela quem a politica de Row Level Security realmente protege.
--
-- Trocar as senhas abaixo em qualquer ambiente que nao seja dev local.

CREATE ROLE agendio_owner WITH LOGIN PASSWORD 'agendio_owner_dev_only' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
CREATE ROLE agendio_app   WITH LOGIN PASSWORD 'agendio_dev_only'       NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

CREATE DATABASE agendio OWNER agendio_owner;

\c agendio

-- Extensoes usadas pelo dominio:
--   btree_gist -> exclusion constraint que impede overbooking (Sprint 3).
--   vector     -> preparacao para busca semantica/IA (Sprint 3+ do roadmap de IA).
--                 Habilitado agora porque a imagem usada (pgvector/pgvector) ja
--                 traz o binario; adicionar depois exigiria migration extra sem
--                 ganho nenhum de esperar.
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE EXTENSION IF NOT EXISTS vector;

GRANT CONNECT ON DATABASE agendio TO agendio_app;
GRANT USAGE ON SCHEMA public TO agendio_app;

-- "IN SCHEMA" omitido de proposito: aplica a QUALQUER schema que agendio_owner
-- vier a criar depois (identity, tenancy, e os que os proximos modulos
-- trouxerem) — os schemas ainda nao existem neste momento, so serao criados
-- pelas migrations de cada modulo.
ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT USAGE ON SCHEMAS TO agendio_app;
ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO agendio_app;
ALTER DEFAULT PRIVILEGES FOR ROLE agendio_owner GRANT USAGE, SELECT ON SEQUENCES TO agendio_app;
