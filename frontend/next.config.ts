import type { NextConfig } from "next";

// resolveAssetUrl (lib/api/client.ts) monta URLs absolutas de imagem apontando
// pra origem da API (logo/banner do tenant, foto de servico/recurso) -- sem
// liberar esse host aqui, o next/image recusa carregar (hostname nao
// configurado), deixando toda imagem vinda do backend quebrada. Le a mesma
// env var usada no client em vez de hardcodar o host, pra acompanhar qualquer
// ambiente (dev/staging/producao) sem precisar editar isto de novo.
const apiUrl = new URL(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5071");

const nextConfig: NextConfig = {
  // Deploy em Docker: gera .next/standalone com so os arquivos rastreados
  // (node_modules minimo incluso) -- sem isto a imagem final precisaria do
  // node_modules inteiro do monorepo.
  output: "standalone",
  images: {
    remotePatterns: [
      {
        protocol: apiUrl.protocol === "https:" ? "https" : "http",
        hostname: apiUrl.hostname,
        port: apiUrl.port,
      },
    ],
  },
};

export default nextConfig;
