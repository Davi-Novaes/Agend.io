import type { Metadata } from "next";
import Link from "next/link";

import { Logo } from "@/components/logo";

export const metadata: Metadata = {
  title: "Termos de Uso — AgendioBR",
};

const LAST_UPDATED = "16 de setembro de 2026";

export default function TermsOfUsePage() {
  return (
    <main className="mx-auto flex min-h-full w-full max-w-2xl flex-col gap-6 p-6 sm:p-10">
      <Logo />
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Termos de Uso</h1>
        <p className="text-muted-foreground mt-1 text-sm">Última atualização: {LAST_UPDATED}</p>
      </div>

      <div className="text-foreground/90 space-y-6 text-sm leading-relaxed">
        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">1. Sobre estes Termos</h2>
          <p>
            Estes Termos de Uso regulam o acesso e a utilização da plataforma AgendioBR (&ldquo;Plataforma&rdquo;, &ldquo;nós&rdquo;),
            operada por <strong>Davi Rodrigues Monteiro de Novaes Tecnologia da Informação</strong>, inscrita no
            CNPJ sob o nº <strong>54.594.038/0001-69</strong>, com sede na Rua Scutum, Jardim Satélite, São José
            dos Campos/SP, CEP 12230-530. Ao criar uma conta, você
            (&ldquo;Cliente&rdquo;, &ldquo;estabelecimento&rdquo;) declara que leu, entendeu e concorda integralmente com estes Termos e com
            a nossa{" "}
            <Link href="/privacidade" className="text-primary underline-offset-4 hover:underline">
              Política de Privacidade
            </Link>
            .
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">2. O que é a Plataforma</h2>
          <p>
            O AgendioBR é um sistema de gestão para negócios baseados em agendamento (barbearias, clínicas,
            estúdios, pet shops, consultorias e segmentos similares), oferecendo agenda online, cadastro de
            clientes, equipe, controle financeiro e de estoque, relatórios e um assistente de apoio ao uso do
            sistema. A Plataforma é fornecida no modelo SaaS (&ldquo;Software as a Service&rdquo;), acessada pela internet,
            sem necessidade de instalação local.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">3. Cadastro e responsabilidade pela conta</h2>
          <p>
            Para usar a Plataforma, é necessário criar uma conta vinculada a um estabelecimento, informando dados
            verdadeiros, completos e atualizados. Você é responsável por manter a confidencialidade da sua senha e
            por todas as atividades realizadas na sua conta. Cada estabelecimento tem seus dados isolados dos
            demais — nenhum dono ou colaborador de um estabelecimento tem acesso aos dados de outro. Contas podem
            ter dois níveis de acesso: <strong>Proprietário</strong> (acesso completo, incluindo dados financeiros
            e da assinatura) e <strong>Colaborador</strong> (acesso operacional, definido pelo Proprietário).
          </p>
          <p>
            Você deve nos avisar imediatamente sobre qualquer uso não autorizado da sua conta. Não nos
            responsabilizamos por perdas decorrentes do uso indevido de credenciais que não tenham sido
            devidamente protegidas por você.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">4. Planos, cobrança e cancelamento</h2>
          <p>
            A Plataforma é oferecida por assinatura, com planos e limites de uso (unidades, profissionais, clientes
            cadastrados) descritos na página de planos no momento da contratação. Novas contas têm direito a um
            período de teste gratuito; encerrado esse período, a cobrança recorrente é iniciada automaticamente no
            método de pagamento informado, processada por meio de parceiro de pagamentos (ver seção 8).
          </p>
          <p>
            Você pode cancelar a assinatura a qualquer momento, diretamente pelo painel, sem multa. O cancelamento
            interrompe a renovação automática; o acesso à Plataforma permanece até o fim do período já pago. Não
            realizamos reembolso proporcional de períodos parciais já iniciados, salvo quando exigido por lei.
          </p>
          <p>
            Podemos alterar os preços dos planos, sempre com aviso prévio razoável antes da renovação seguinte à
            mudança.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">5. Uso aceitável</h2>
          <p>Ao usar a Plataforma, você concorda em não:</p>
          <ul className="list-disc space-y-1 pl-5">
            <li>Utilizar o serviço para fins ilícitos ou que violem direitos de terceiros;</li>
            <li>Tentar acessar, sem autorização, dados de outro estabelecimento ou áreas administrativas da Plataforma;</li>
            <li>Realizar engenharia reversa, copiar ou explorar comercialmente o software da Plataforma fora do uso pretendido;</li>
            <li>Enviar, por meio dos recursos de comunicação da Plataforma (e-mail, WhatsApp), mensagens não solicitadas a pessoas que não sejam seus próprios clientes cadastrados;</li>
            <li>Sobrecarregar deliberadamente a infraestrutura da Plataforma (ex.: ataques de negação de serviço, automações abusivas).</li>
          </ul>
          <p>
            O descumprimento pode levar à suspensão ou ao encerramento da conta, sem prejuízo de outras medidas
            cabíveis.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">6. Dados inseridos por você</h2>
          <p>
            Você é o responsável pela exatidão dos dados que insere sobre o seu estabelecimento e sobre seus
            próprios clientes finais (nome, telefone, CPF, anotações de saúde quando aplicável ao seu segmento,
            entre outros). Em relação a esses dados de clientes finais, você atua como controlador, nos termos da
            Lei Geral de Proteção de Dados (LGPD), e o AgendioBR atua como operador, processando os dados apenas
            para viabilizar o funcionamento da Plataforma, conforme detalhado na nossa{" "}
            <Link href="/privacidade" className="text-primary underline-offset-4 hover:underline">
              Política de Privacidade
            </Link>
            . Você é responsável por obter, quando exigido pela LGPD, o consentimento ou outra base legal adequada
            dos seus próprios clientes para o tratamento dos dados que você insere na Plataforma.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">7. Propriedade intelectual</h2>
          <p>
            O software, a marca AgendioBR, o layout, os textos e demais elementos da Plataforma são de nossa
            propriedade ou licenciados a nós, protegidos por leis de propriedade intelectual. Estes Termos não
            transferem a você nenhum direito sobre esses elementos, apenas uma licença limitada, não exclusiva e
            revogável de uso da Plataforma enquanto sua assinatura estiver ativa. Os dados que você insere (do seu
            estabelecimento e dos seus clientes) continuam sendo seus.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">8. Serviços de terceiros</h2>
          <p>
            A Plataforma utiliza serviços de terceiros para funcionar — processamento de pagamentos, envio de
            e-mails transacionais, envio de mensagens via WhatsApp (quando você optar por usar esse recurso) e
            infraestrutura de hospedagem em nuvem. O uso desses serviços está sujeito também aos termos próprios de
            cada parceiro. Detalhes sobre quais dados são compartilhados com cada um estão na nossa{" "}
            <Link href="/privacidade" className="text-primary underline-offset-4 hover:underline">
              Política de Privacidade
            </Link>
            .
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">9. Disponibilidade e suporte</h2>
          <p>
            Envidamos os melhores esforços para manter a Plataforma disponível de forma contínua, mas não
            garantimos disponibilidade ininterrupta — podem ocorrer manutenções programadas ou interrupções
            eventuais por causas fora do nosso controle. Não nos responsabilizamos por perdas decorrentes de
            indisponibilidade temporária.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">10. Limitação de responsabilidade</h2>
          <p>
            Na máxima extensão permitida pela lei, não nos responsabilizamos por danos indiretos, lucros cessantes
            ou perda de dados decorrentes do uso da Plataforma, exceto nos casos de dolo ou culpa grave de nossa
            parte. Nossa responsabilidade total, quando aplicável, está limitada ao valor pago por você nos 12
            (doze) meses anteriores ao evento que originou a reclamação.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">11. Encerramento</h2>
          <p>
            Você pode encerrar sua conta a qualquer momento pelo painel. Podemos suspender ou encerrar contas que
            violem estes Termos, mediante aviso prévio quando possível. Após o encerramento, seus dados são
            mantidos pelo prazo descrito na Política de Privacidade e então eliminados, ressalvadas obrigações
            legais de retenção.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">12. Alterações destes Termos</h2>
          <p>
            Podemos atualizar estes Termos periodicamente. Alterações relevantes serão comunicadas por e-mail ou
            aviso na Plataforma com antecedência razoável. O uso continuado da Plataforma após a entrada em vigor
            das alterações implica concordância com os novos Termos.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">13. Lei aplicável e foro</h2>
          <p>
            Estes Termos são regidos pelas leis da República Federativa do Brasil. Fica eleito o foro da comarca de{" "}
            <strong>São José dos Campos/SP</strong> para dirimir eventuais controvérsias, com renúncia a qualquer outro, por
            mais privilegiado que seja.
          </p>
        </section>

        <section className="space-y-2">
          <h2 className="text-foreground font-semibold">14. Contato</h2>
          <p>
            Dúvidas sobre estes Termos podem ser enviadas para <strong>agendio.ai@gmail.com</strong>.
          </p>
        </section>
      </div>

      <Link href="/onboarding" className="text-primary text-sm underline-offset-4 hover:underline">
        Voltar para o cadastro
      </Link>
    </main>
  );
}
