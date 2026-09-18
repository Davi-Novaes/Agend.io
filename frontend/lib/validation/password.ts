import { z } from "zod";

// Mesma regra do backend (ver PasswordValidationRules.cs no modulo Identity):
// minimo 10 caracteres, 1 maiuscula, 1 simbolo -- compartilhado por toda tela
// que define/troca senha (cadastro, redefinicao, troca de senha, aceite de
// convite) pra nao duplicar a regex e a mensagem em 4 lugares.
export const strongPasswordSchema = z
  .string()
  .min(10, "A senha precisa ter pelo menos 10 caracteres.")
  .regex(/[A-Z]/, "A senha precisa ter pelo menos uma letra maiúscula.")
  .regex(/[^a-zA-Z0-9]/, "A senha precisa ter pelo menos um símbolo (ex.: ! @ # $ %).");
