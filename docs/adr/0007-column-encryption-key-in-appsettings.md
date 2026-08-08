# 0007 — Chave de criptografia de coluna em appsettings, sem KMS

## Status
Aceito (Sprint 8).

## Contexto
CLAUDE.md exige que dado sensível (CPF, dado de saúde) fique "criptografado em
coluna". Até este sprint nenhum campo desse tipo existia no schema — `Customer`
tinha só um `CustomData` livre em jsonb, sem criptografia nenhuma. Não existe
Key Vault, AWS Secrets Manager ou qualquer abstração de gerenciamento de chaves
neste projeto: todo segredo hoje (`Jwt:SigningKey`, `Asaas:ApiKey`) já vive como
valor plano em `appsettings.Development.json`, bound via `IOptions<T>`, com
comentário "troque em produção" e a expectativa de que produção sobrescreva via
variável de ambiente/secret store.

## Decisão
`ColumnEncryptionOptions.Key` (base64, 32 bytes) segue exatamente o mesmo
padrão: nenhuma integração com KMS, nenhuma rotação de chave — uma chave
estática, criptografia AES-256-GCM aplicada só na fronteira do EF Core
(`EncryptedStringConverter`, um `ValueConverter<string?, string?>` reutilizável
por qualquer módulo). Aplicado hoje em `Customer.Cpf` e `Customer.HealthNotes`
(e, no mesmo sprint, em `User.MfaSecretEncrypted` — ver ADR 0008).

Como `IEntityTypeConfiguration<T>` é instanciado sem parâmetro por
`ApplyConfigurationsFromAssembly()`, o conversor (que depende de
`IEncryptionService`) não pode entrar lá — cada `DbContext` de módulo que
precisa dele recebe `IEncryptionService` via construtor (mesmo padrão já usado
para `ITenantContext`) e aplica `HasConversion(...)` direto no
`OnModelCreating`, depois do `ApplyConfigurationsFromAssembly()` rodar.

## Consequências
- **Coluna criptografada não é filtrável nem ordenável em SQL.** AES-GCM usa um
  nonce aleatório a cada `Encrypt`, então o mesmo texto plano produz ciphertext
  diferente a cada escrita — `WHERE cpf = ...` ou `ORDER BY cpf` nunca vão
  funcionar do jeito que funcionariam numa coluna comum. Nenhum código hoje
  depende disso; se algum dia precisar, é sinal de que a coluna errada foi
  escolhida para ficar criptografada, não que o conversor precisa mudar.
- **Sem rotação de chave.** Trocar `ColumnEncryption:Key` torna ilegível todo
  dado já persistido — rotação de verdade exigiria uma migration de
  re-criptografia que não existe. Aceitável para o estágio atual do produto,
  registrado aqui para não ser esquecido.
- Sem coluna, sem HasMaxLength no valor convertido: o tamanho armazenado
  (base64 de nonce+tag+ciphertext) é maior que o texto original, e calcular
  esse overhead certinho não vale o risco de truncar um CPF por engano — a
  coluna fica `text` sem limite, o limite de tamanho já é aplicado no texto
  plano via FluentValidation antes de chegar aqui.
- `Agendio.IntegrationTests.CustomerEncryptionTests` lê a coluna crua via
  conexão direta (bypassando API e EF Core) para provar que o valor persistido
  não é o texto plano — não basta o roundtrip via API "dar certo", porque um
  conversor passthrough por engano passaria nesse teste sozinho.
