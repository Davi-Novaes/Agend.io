namespace Agendio.SharedKernel.Messaging;

/// <summary>
/// Marcador generico unificando Command e Query para o dispatcher. Nao e usado
/// diretamente pelo codigo de aplicacao — use ICommand, ICommand&lt;T&gt; ou IQuery&lt;T&gt;.
/// </summary>
public interface IRequest<TResponse>;
