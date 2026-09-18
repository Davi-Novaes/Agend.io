import type { Metadata } from "next";
import Link from "next/link";

import { Logo } from "@/components/logo";

export const metadata: Metadata = {
  title: "Política de Privacidade — AgendioBR",
};

const LAST_UPDATED = "16 de setembro de 2026";

export default function PrivacyPolicyPage() {
  return (
    <main className="mx-auto flex min-h-full w-full max-w-2xl flex-col gap-6 p-6 sm:p-10">
      <Logo />
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Política de Privacidade</h1>
        <p className="text-muted-foreground mt-1 text-sm">Última atualização: {LAST_UPDATED}</p>
      </div>

      <div className="text-foreground/90 space-y-6 text-sm leading-relaxed">
        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">1. Introdução</h2>
          <p>
            Esta Política de Privacidade descreve como{" "}
            <strong>Davi Rodrigues Monteiro de Novaes Tecnologia da Informação</strong> (CNPJ{" "}
            <strong>54.594.038/0001-69</strong>), operadora da plataforma AgendioBR, coleta, usa, armazena e protege dados
            pessoais, em conformidade com a Lei nº 13.709/2018 (Lei Geral de Proteção de Dados — LGPD).
          </p>
          <p>
            O AgendioBR é uma plataforma multi-tenant: cada estabelecimento cliente (dono do negócio que contrata o
            AgendioBR) tem seus próprios dados e os de seus clientes finais isolados dos demais estabelecimentos,
            tanto em nível de aplicação quanto de banco de dados.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">2. Controlador e operador — dois papéis distintos</h2>
          <p>
            Em relação aos dados da <strong>sua própria conta</strong> (você, dono ou colaborador de um
            estabelecimento: nome, e-mail, telefone, CPF/CNPJ, dados de cobrança), o AgendioBR é o{" "}
            <strong>controlador</strong> dos dados.
          </p>
          <p>
            Em relação aos dados dos <strong>clientes finais do seu estabelecimento</strong> que você cadastra na
            Plataforma (nome, telefone, CPF quando informado, anotações de saúde quando aplicável ao seu segmento),
            o <strong>estabelecimento é o controlador</strong> e o AgendioBR atua apenas como{" "}
            <strong>operador</strong>, tratando esses dados unicamente para viabilizar o funcionamento da
            Plataforma, conforme suas instruções.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">3. Quais dados coletamos</h2>
          <p className="font-medium">Da conta do estabelecimento (dono e colaboradores):</p>
          <ul className="list-disc space-y-1 pl-5">
            <li>Nome completo, e-mail, telefone, CPF ou CNPJ;</li>
            <li>Senha (armazenada apenas como hash criptográfico Argon2id — nunca em texto plano);</li>
            <li>Nome do estabelecimento, segmento, fuso horário, cores e identidade visual configuradas;</li>
            <li>Dados de cobrança da assinatura (processados pelo parceiro de pagamentos, ver seção 5 — não armazenamos número completo de cartão de crédito);</li>
            <li>Registros técnicos de acesso (IP, navegador, data/hora de login) para segurança da conta.</li>
          </ul>
          <p className="font-medium">Dos clientes finais do estabelecimento (inseridos pelo próprio estabelecimento):</p>
          <ul className="list-disc space-y-1 pl-5">
            <li>Nome, telefone e, opcionalmente, e-mail;</li>
            <li>CPF, quando o estabelecimento optar por registrá-lo;</li>
            <li>
              Anotações de saúde (ex.: alergias, restrições, convênio) — dado sensível nos termos da LGPD, aplicável
              a segmentos como clínicas e profissionais de saúde, sempre criptografado em banco;
            </li>
            <li>Histórico de agendamentos, serviços contratados e pontos de fidelidade.</li>
          </ul>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">4. Bases legais de tratamento</h2>
          <p>Tratamos dados pessoais com fundamento em uma ou mais das seguintes bases legais (art. 7º e 11 da LGPD):</p>
          <ul className="list-disc space-y-1 pl-5">
            <li><strong>Execução de contrato</strong>: para viabilizar o uso da Plataforma pelo estabelecimento contratante;</li>
            <li><strong>Cumprimento de obrigação legal ou regulatória</strong>: ex. dados fiscais de cobrança;</li>
            <li><strong>Legítimo interesse</strong>: para segurança, prevenção a fraudes e melhoria do serviço, sempre de forma proporcional e sem se sobrepor aos direitos do titular;</li>
            <li><strong>Consentimento</strong>: quando exigido, especialmente para o tratamento de dados sensíveis (ex.: anotações de saúde), cuja obtenção é responsabilidade do estabelecimento perante seu próprio cliente final.</li>
          </ul>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">5. Com quem compartilhamos dados</h2>
          <p>Não vendemos dados pessoais. Compartilhamos dados, na medida do necessário, com:</p>
          <ul className="list-disc space-y-1 pl-5">
            <li><strong>Processador de pagamentos</strong>: para cobrança da assinatura e repasses financeiros, recebendo os dados de cobrança necessários à transação;</li>
            <li><strong>Provedor de envio de e-mail</strong>: para e-mails transacionais (confirmação de cadastro, redefinição de senha, notificações);</li>
            <li><strong>WhatsApp/Meta</strong>: somente quando o estabelecimento optar por usar o recurso de mensagens via WhatsApp, para envio de lembretes e comunicações aos próprios clientes do estabelecimento;</li>
            <li><strong>Provedores de infraestrutura em nuvem</strong>: para hospedagem da aplicação e do banco de dados;</li>
            <li><strong>Autoridades públicas</strong>: quando exigido por lei, ordem judicial ou requisição de autoridade competente.</li>
          </ul>
          <p>
            Alguns desses provedores podem processar dados em servidores localizados fora do Brasil. Nesses casos,
            adotamos as salvaguardas exigidas pela LGPD para transferência internacional de dados.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">6. Segurança da informação</h2>
          <p>Adotamos medidas técnicas e organizacionais para proteger os dados, entre elas:</p>
          <ul className="list-disc space-y-1 pl-5">
            <li>Senhas protegidas com hash Argon2id, nunca armazenadas em texto plano;</li>
            <li>CPF e anotações de saúde criptografados em banco de dados (AES-256);</li>
            <li>Isolamento de dados entre estabelecimentos em duas camadas independentes (filtro de aplicação e Row Level Security no banco de dados);</li>
            <li>Tokens de acesso de curta duração (15 minutos), mantidos apenas em memória no navegador — nunca em armazenamento persistente do dispositivo;</li>
            <li>Token de renovação de sessão armazenado como hash, entregue em cookie protegido (HttpOnly, Secure);</li>
            <li>Nunca registramos senha, token de acesso, CPF ou dado de saúde em nossos logs internos.</li>
          </ul>
          <p>
            Nenhum sistema é 100% imune a incidentes. Em caso de incidente de segurança que envolva risco relevante
            aos titulares, notificaremos a Autoridade Nacional de Proteção de Dados (ANPD) e os afetados, conforme
            exigido pela LGPD.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">7. Cookies</h2>
          <p>
            Utilizamos apenas cookies essenciais ao funcionamento da Plataforma (ex.: o cookie que mantém sua sessão
            de forma segura). Não utilizamos cookies de rastreamento de terceiros para publicidade.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">8. Retenção e eliminação de dados</h2>
          <p>
            Mantemos os dados pessoais pelo tempo necessário ao cumprimento das finalidades descritas nesta
            Política, ou pelo prazo exigido por obrigações legais e fiscais (ex.: dados de cobrança), o que for
            maior. Encerrada a conta do estabelecimento, os dados são eliminados ou anonimizados após o prazo de
            retenção aplicável, exceto quando devamos preservá-los para cumprimento de obrigação legal, exercício
            regular de direitos em processo judicial ou administrativo, ou uso legítimo estritamente necessário para
            prevenção a fraudes.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">9. Seus direitos como titular de dados</h2>
          <p>Nos termos do art. 18 da LGPD, você pode solicitar, a qualquer momento:</p>
          <ul className="list-disc space-y-1 pl-5">
            <li>Confirmação da existência de tratamento e acesso aos seus dados;</li>
            <li>Correção de dados incompletos, inexatos ou desatualizados;</li>
            <li>Anonimização, bloqueio ou eliminação de dados desnecessários ou tratados em desconformidade com a lei;</li>
            <li>Portabilidade dos dados a outro fornecedor de serviço;</li>
            <li>Eliminação dos dados pessoais tratados com base no seu consentimento;</li>
            <li>Informação sobre com quem compartilhamos seus dados;</li>
            <li>Revogação do consentimento, quando essa for a base legal do tratamento.</li>
          </ul>
          <p>
            Se você é <strong>cliente final de um estabelecimento</strong> que usa o AgendioBR (não o dono da
            conta), o estabelecimento é o controlador dos seus dados — recomendamos que o contato inicial seja
            diretamente com ele. Caso prefira, também pode nos contatar pelo canal abaixo e faremos a intermediação
            necessária.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">10. Crianças e adolescentes</h2>
          <p>
            A Plataforma se destina à contratação e uso por profissionais e empresas (maiores de 18 anos). Dados de
            clientes finais menores de idade podem ser inseridos pelo estabelecimento (ex.: agendamento de um
            atendimento infantil) sob a responsabilidade e consentimento do responsável legal, cabendo ao
            estabelecimento assegurar essa condição.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">11. Alterações desta Política</h2>
          <p>
            Podemos atualizar esta Política periodicamente para refletir mudanças legais ou no funcionamento da
            Plataforma. Alterações relevantes serão comunicadas por e-mail ou aviso na Plataforma.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">12. Encarregado de Dados (DPO) e contato</h2>
          <p>
            Para exercer seus direitos ou tirar dúvidas sobre o tratamento de dados pessoais, entre em contato com
            nosso Encarregado de Proteção de Dados pelo e-mail <strong>agendio.ai@gmail.com</strong>.
          </p>
        </section>
      </div>

      <Link href="/onboarding" className="text-primary text-sm underline-offset-4 hover:underline">
        Voltar para o cadastro
      </Link>
    </main>
  );
}
