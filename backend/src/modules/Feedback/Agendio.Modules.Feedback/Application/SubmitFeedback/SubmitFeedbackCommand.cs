using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Feedback.Application.SubmitFeedback;

public sealed record SubmitFeedbackCommand(Guid SubmittedByUserId, string Subject, string Body) : ICommand;
