import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

// Trocar de dominio = mesmo find/replace do resto do projeto (ver CLAUDE.md).
const ROOT_DOMAIN = process.env.NEXT_PUBLIC_ROOT_DOMAIN ?? "agendiobr.com.br";

// Subdominios que nunca sao slug de tenant -- mantido em sincronia manual com
// ReservedSlugs (backend/.../Tenancy/Domain/ReservedSlugs.cs), que e quem
// realmente impede o cadastro. Aqui so evita reescrever a URL de um
// subdominio que nunca poderia corresponder a um tenant real.
const RESERVED_SUBDOMAINS = new Set([
  "www", "api", "app", "admin", "painel", "platform", "superadmin", "root",
  "mail", "email", "smtp", "send", "ftp", "ns1", "ns2", "cdn", "static", "assets",
  "blog", "docs", "status", "support", "help", "dashboard",
  "test", "staging", "dev", "localhost",
]);

// Fora do dominio de producao (dev local, preview, etc.) a pagina publica do
// tenant continua acessivel por path (/barber-teste) -- ver app/(public)/[slug].
// So reescreve para path interno quando o Host bate com um subdominio de
// primeiro nivel de ROOT_DOMAIN que nao e reservado.
export function proxy(request: NextRequest) {
  const hostname = (request.headers.get("host") ?? "").split(":")[0];

  if (!hostname.endsWith(`.${ROOT_DOMAIN}`)) {
    return NextResponse.next();
  }

  const subdomain = hostname.slice(0, -(ROOT_DOMAIN.length + 1));

  if (!subdomain || subdomain.includes(".") || RESERVED_SUBDOMAINS.has(subdomain)) {
    return NextResponse.next();
  }

  const url = request.nextUrl.clone();
  url.pathname = url.pathname === "/" ? `/${subdomain}` : `/${subdomain}${url.pathname}`;
  return NextResponse.rewrite(url);
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)"],
};
