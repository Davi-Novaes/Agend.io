namespace Agendio.IntegrationTests;

/// <summary>
/// Uma unica instancia de IntegrationTestFixture (e portanto um unico conjunto
/// de containers Postgres/Redis/RabbitMQ) compartilhada entre todas as classes
/// de teste desta collection — subir os tres containers de novo por classe
/// seria lento sem ganho de isolamento real (cada teste ja usa tenant/e-mail
/// unicos via Guid, entao os dados nao colidem mesmo compartilhando o banco).
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>;
