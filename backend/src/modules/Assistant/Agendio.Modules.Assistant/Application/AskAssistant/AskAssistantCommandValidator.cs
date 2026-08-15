using FluentValidation;

namespace Agendio.Modules.Assistant.Application.AskAssistant;

public sealed class AskAssistantCommandValidator : AbstractValidator<AskAssistantCommand>
{
    public AskAssistantCommandValidator()
    {
        RuleFor(c => c.Question).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.History).Must(h => h.Count <= 20).WithMessage("Historico de conversa muito longo.");
        RuleForEach(c => c.History).ChildRules(message =>
        {
            message.RuleFor(m => m.Role).Must(r => r is "user" or "assistant").WithMessage("Role deve ser 'user' ou 'assistant'.");
            message.RuleFor(m => m.Text).NotEmpty().MaximumLength(4000);
        });
    }
}
