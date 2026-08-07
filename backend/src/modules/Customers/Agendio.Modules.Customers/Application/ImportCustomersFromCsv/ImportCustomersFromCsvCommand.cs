using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.ImportCustomersFromCsv;

// Colunas esperadas (cabecalho, qualquer ordem): FullName, Email, Phone, Notes.
// So FullName e obrigatoria.
public sealed record ImportCustomersFromCsvCommand(byte[] CsvContent) : ICommand<ImportCustomersResult>;

public sealed record ImportCustomersResult(int Imported, int Skipped, IReadOnlyList<string> Errors);
