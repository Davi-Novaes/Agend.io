#!/bin/sh
# Roda `dotnet ef database update` para cada modulo (cada um tem seu proprio
# DbContext/pasta de Migrations -- nao existe uma migration "global"). Pensado
# para rodar dentro do estagio `build` da imagem backend (tem o SDK + o
# codigo-fonte completo), contra a rede docker compose de producao:
#
#   docker compose -f infra/docker-compose.prod.yml run --rm migrator
set -e

export PATH="$PATH:/root/.dotnet/tools"
if ! command -v dotnet-ef >/dev/null 2>&1; then
	dotnet tool install --global dotnet-ef --version 10.*
fi

MODULES="Assistant Billing Catalog Customers Estoque Feedback Financeiro Identity Marketing Platform Resources Scheduling Tenancy"

for m in $MODULES; do
	echo "==> Migrando $m"
	dotnet ef database update \
		--project "src/modules/$m/Agendio.Modules.$m" \
		--startup-project "src/host/Agendio.Api"
done

echo "==> Todas as migrations aplicadas."
