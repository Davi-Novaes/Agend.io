# Monitor de Recursos da Máquina

Mini sistema de monitoramento de recursos em tempo real: CPU, memória, disco, rede e processos.

Um único arquivo Python (`app.py`), sem Flask nem dependências pesadas — só a biblioteca padrão + `psutil`.

## Como rodar

```bash
pip install psutil
python app.py
```

Depois abra **http://localhost:8765** no navegador.

## O que ele monitora (atualiza a cada 1s, sem recarregar a página)

- **CPU** — uso total, uso por núcleo, frequência, load average e histórico gráfico
- **Memória** — RAM usada/total com histórico
- **Disco** — espaço usado e taxa de leitura/escrita
- **Rede** — taxa de upload/download e total transferido
- **Processos** — top 6 por consumo de CPU

## Teste de velocidade da internet

O card "Velocidade da Internet" mede ping, jitter, download e upload de verdade,
parecido com o Speedtest — clique em "Iniciar Teste" e acompanhe ao vivo.

Usa os servidores públicos de teste da Cloudflare (`speed.cloudflare.com`), com
conexão HTTPS direta via biblioteca padrão (sem dependências extras). Baixa
~25 MB e envia ~10 MB para medir a velocidade — como é uma única conexão (sem
paralelismo), em internet muito rápida (500+ Mbps) o número pode ficar um
pouco abaixo do real.

## Parar o servidor

`Ctrl+C` no terminal onde ele está rodando.
