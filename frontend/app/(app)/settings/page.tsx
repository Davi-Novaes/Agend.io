import { redirect } from "next/navigation";

// /settings sozinho nao tem conteudo proprio -- so existe pra bookmarks/links
// antigos nao quebrarem. A Sidebar aponta direto pra cada rota (Seguranca,
// Minha conta, Empresa -> ..., Marca -> ...), nao mais pra um hub unico.
export default function SettingsIndexPage() {
  redirect("/settings/security");
}
