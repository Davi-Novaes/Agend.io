using FluentValidation;

namespace Agendio.Modules.Customers.Application.ImportCustomersFromCsv;

public sealed class ImportCustomersFromCsvCommandValidator : AbstractValidator<ImportCustomersFromCsvCommand>
{
    private const int MaxSizeBytes = 5 * 1024 * 1024;

    public ImportCustomersFromCsvCommandValidator()
    {
        RuleFor(c => c.CsvContent).NotEmpty();
        RuleFor(c => c.CsvContent.Length)
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage("O arquivo CSV nao pode ter mais que 5MB.");
    }
}
