using System.Globalization;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using CsvHelper;
using CsvHelper.Configuration;

namespace Agendio.Modules.Customers.Application.ImportCustomersFromCsv;

public sealed class ImportCustomersFromCsvCommandHandler(CustomersDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<ImportCustomersFromCsvCommand, ImportCustomersResult>
{
    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        MissingFieldFound = null,
        HeaderValidated = null,
    };

    public async Task<Result<ImportCustomersResult>> Handle(ImportCustomersFromCsvCommand request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(new MemoryStream(request.CsvContent));
        using var csv = new CsvReader(reader, CsvConfig);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
        {
            return Result.Failure<ImportCustomersResult>(
                Error.Validation("Customer.EmptyCsv", "O arquivo CSV esta vazio ou sem cabecalho."));
        }

        var errors = new List<string>();
        var imported = 0;
        var skipped = 0;
        var rowNumber = 1;

        while (await csv.ReadAsync())
        {
            rowNumber++;

            csv.TryGetField("fullname", out string? fullName);
            csv.TryGetField("email", out string? email);
            csv.TryGetField("phone", out string? phone);
            csv.TryGetField("notes", out string? notes);

            var customerResult = Customer.Create(tenantContext.TenantId, fullName, email, phone, notes, dateOfBirth: null);
            if (customerResult.IsFailure)
            {
                skipped++;
                errors.Add($"Linha {rowNumber}: {customerResult.Error.Message}");
                continue;
            }

            dbContext.Customers.Add(customerResult.Value);
            imported++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportCustomersResult(imported, skipped, errors));
    }
}
