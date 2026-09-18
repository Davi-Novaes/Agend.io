import type { MetadataRoute } from "next";
import { SITE_URL } from "@/app/layout";

// So as paginas publicas de conteudo (nao autenticadas, nao transacionais)
// -- login/onboarding/reset-password/confirm-email/invitations sao paginas
// de formulario/token, sem valor de indexacao, ficam fora de proposito.
export default function sitemap(): MetadataRoute.Sitemap {
  const lastModified = new Date();

  return [
    { url: SITE_URL, lastModified, changeFrequency: "weekly", priority: 1 },
    { url: `${SITE_URL}/onboarding`, lastModified, changeFrequency: "monthly", priority: 0.8 },
    { url: `${SITE_URL}/termos`, lastModified, changeFrequency: "yearly", priority: 0.3 },
    { url: `${SITE_URL}/privacidade`, lastModified, changeFrequency: "yearly", priority: 0.3 },
  ];
}
