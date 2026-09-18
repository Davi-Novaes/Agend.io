import type { MetadataRoute } from "next";
import { SITE_URL } from "@/app/layout";

// Bloqueia tudo que exige sessao autenticada (o crawler so bateria numa tela
// de login mesmo) ou e transacional/token-unico -- lista espelha as rotas
// reais de app/(app) e app/(public), ver nav-config.ts para a navegacao
// completa do painel.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/painel",
        "/agenda",
        "/waitlist",
        "/clientes",
        "/servicos",
        "/recursos",
        "/estoque",
        "/financeiro",
        "/relatorios",
        "/marketing",
        "/settings",
        "/admin",
        "/confirm-email",
        "/invitations",
        "/api/",
      ],
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
