// Extraido de site-header.tsx (Client Component) pra um modulo sem "use
// client": um array de dados exportado de um Client Component nao atravessa
// a fronteira server/client de forma confiavel no App Router -- vira um
// client reference opaco quando importado por Server Component (ver
// app/(marketing)/page.tsx, que precisa dos mesmos links no rodape).
export const NAV_LINKS = [
  { href: "#showcase", label: "Produto" },
  { href: "#funcionalidades", label: "Recursos" },
  { href: "#como-funciona", label: "Como funciona" },
  { href: "#precos", label: "Planos" },
];
