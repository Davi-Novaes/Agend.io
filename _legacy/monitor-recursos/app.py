"""Mini sistema de monitoramento de recursos da maquina em tempo real.

Um unico arquivo Python: usa apenas a biblioteca padrao + psutil.
Sobe um servidor HTTP local que serve um dashboard e uma API JSON.
"""

import http.client
import json
import os
import ssl
import time
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import psutil

HOST = "127.0.0.1"
PORT = 8765

# Estado global para calcular taxas (bytes/s) de disco e rede entre chamadas
_lock = threading.Lock()
_state = {
    "last_time": time.time(),
    "last_disk": psutil.disk_io_counters(),
    "last_net": psutil.net_io_counters(),
    "cpu_total": 0.0,
    "cpu_per_core": [],
}

# psutil.cpu_percent precisa de uma primeira chamada "descartada" para calibrar
psutil.cpu_percent(percpu=True)
for p in psutil.process_iter(["pid"]):
    try:
        p.cpu_percent(None)
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        pass

CPU_COUNT = psutil.cpu_count() or 1


def _cpu_sampler():
    """Amostra a CPU numa janela real de 1s, isolada dos ciclos de requisicao HTTP.

    Se a leitura ficasse acoplada ao request (varios clientes chamando a API ao
    mesmo tempo), cada chamada reseta o timer interno do psutil.cpu_percent e o
    intervalo medido vira uma fracao minuscula de segundo em vez de 1s cheio,
    fazendo o valor cair para perto de 0 sem motivo real.
    """
    while True:
        per_core = psutil.cpu_percent(interval=1, percpu=True)
        with _lock:
            _state["cpu_per_core"] = per_core
            _state["cpu_total"] = sum(per_core) / len(per_core) if per_core else 0.0


threading.Thread(target=_cpu_sampler, daemon=True).start()


def get_load_avg():
    try:
        one, five, fifteen = psutil.getloadavg()
        return {"1min": round(one, 2), "5min": round(five, 2), "15min": round(fifteen, 2)}
    except (AttributeError, OSError):
        return None


def get_stats():
    with _lock:
        now = time.time()
        elapsed = max(now - _state["last_time"], 1e-6)

        # CPU (lido do cache preenchido pela thread _cpu_sampler)
        cpu_percent_total = round(_state["cpu_total"], 1)
        cpu_per_core = _state["cpu_per_core"]
        try:
            freq = psutil.cpu_freq()
            cpu_freq = round(freq.current, 0) if freq else None
        except Exception:
            cpu_freq = None

        # Memoria
        mem = psutil.virtual_memory()

        # Disco (uso do disco principal + taxa de I/O)
        disk_usage = psutil.disk_usage("/")
        disk_io = psutil.disk_io_counters()
        disk_read_rate = 0
        disk_write_rate = 0
        if disk_io and _state["last_disk"]:
            disk_read_rate = (disk_io.read_bytes - _state["last_disk"].read_bytes) / elapsed
            disk_write_rate = (disk_io.write_bytes - _state["last_disk"].write_bytes) / elapsed

        # Rede
        net_io = psutil.net_io_counters()
        net_up_rate = 0
        net_down_rate = 0
        if net_io and _state["last_net"]:
            net_up_rate = (net_io.bytes_sent - _state["last_net"].bytes_sent) / elapsed
            net_down_rate = (net_io.bytes_recv - _state["last_net"].bytes_recv) / elapsed

        _state["last_time"] = now
        _state["last_disk"] = disk_io
        _state["last_net"] = net_io

        # Top processos por uso de CPU
        procs = []
        for p in psutil.process_iter(["pid", "name", "memory_percent"]):
            try:
                cpu = p.cpu_percent(None) / CPU_COUNT
                procs.append({
                    "pid": p.info["pid"],
                    "name": p.info["name"] or "?",
                    "cpu": round(cpu, 1),
                    "mem": round(p.info["memory_percent"] or 0, 1),
                })
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
        procs.sort(key=lambda x: x["cpu"], reverse=True)
        top_procs = procs[:6]

        return {
            "timestamp": now,
            "cpu": {
                "percent": cpu_percent_total,
                "per_core": cpu_per_core,
                "freq_mhz": cpu_freq,
                "load_avg": get_load_avg(),
            },
            "memory": {
                "percent": mem.percent,
                "used": mem.used,
                "total": mem.total,
            },
            "disk": {
                "percent": disk_usage.percent,
                "used": disk_usage.used,
                "total": disk_usage.total,
                "read_rate": disk_read_rate,
                "write_rate": disk_write_rate,
            },
            "network": {
                "upload_rate": net_up_rate,
                "download_rate": net_down_rate,
                "total_sent": net_io.bytes_sent if net_io else 0,
                "total_recv": net_io.bytes_recv if net_io else 0,
            },
            "processes": top_procs,
        }


SPEEDTEST_HOST = "speed.cloudflare.com"
SPEEDTEST_DOWNLOAD_BYTES = 25_000_000  # ~25 MB
SPEEDTEST_UPLOAD_BYTES = 10_000_000    # ~10 MB
SPEEDTEST_CHUNK = 65536

_speed_lock = threading.Lock()
_speed_state = {
    "status": "idle",  # idle | testing | done | error
    "phase": None,      # ping | download | upload
    "progress": 0.0,
    "current_mbps": 0.0,
    "ping_ms": None,
    "jitter_ms": None,
    "download_mbps": None,
    "upload_mbps": None,
    "error": None,
    "timestamp": None,
}


def _speed_update(**kwargs):
    with _speed_lock:
        _speed_state.update(kwargs)


def _speed_snapshot():
    with _speed_lock:
        return dict(_speed_state)


def _new_connection():
    ctx = ssl.create_default_context()
    return http.client.HTTPSConnection(SPEEDTEST_HOST, timeout=15, context=ctx)


def _measure_ping():
    samples = []
    for _ in range(6):
        conn = _new_connection()
        start = time.time()
        conn.request("GET", "/__down?bytes=0")
        resp = conn.getresponse()
        resp.read()
        conn.close()
        samples.append((time.time() - start) * 1000)
    ping_ms = round(min(samples), 1)
    if len(samples) > 1:
        diffs = [abs(samples[i] - samples[i - 1]) for i in range(1, len(samples))]
        jitter_ms = round(sum(diffs) / len(diffs), 1)
    else:
        jitter_ms = 0.0
    return ping_ms, jitter_ms


def _measure_download(total_bytes):
    conn = _new_connection()
    conn.request("GET", f"/__down?bytes={total_bytes}")
    resp = conn.getresponse()

    received = 0
    start = time.time()
    last_update = start
    while True:
        chunk = resp.read(SPEEDTEST_CHUNK)
        if not chunk:
            break
        received += len(chunk)
        now = time.time()
        if now - last_update > 0.15:
            elapsed = now - start
            mbps = (received * 8) / (elapsed * 1_000_000) if elapsed > 0 else 0
            _speed_update(
                phase="download",
                progress=round(min(received / total_bytes, 1.0) * 100, 1),
                current_mbps=round(mbps, 1),
            )
            last_update = now
    conn.close()

    elapsed = max(time.time() - start, 1e-6)
    return round((received * 8) / (elapsed * 1_000_000), 1)


def _measure_upload(total_bytes):
    conn = _new_connection()
    conn.putrequest("POST", "/__up")
    conn.putheader("Content-Type", "application/octet-stream")
    conn.putheader("Content-Length", str(total_bytes))
    conn.endheaders()

    chunk = os.urandom(SPEEDTEST_CHUNK)
    sent = 0
    start = time.time()
    last_update = start
    while sent < total_bytes:
        n = min(SPEEDTEST_CHUNK, total_bytes - sent)
        conn.send(chunk[:n])
        sent += n
        now = time.time()
        if now - last_update > 0.15:
            elapsed = now - start
            mbps = (sent * 8) / (elapsed * 1_000_000) if elapsed > 0 else 0
            _speed_update(
                phase="upload",
                progress=round(min(sent / total_bytes, 1.0) * 100, 1),
                current_mbps=round(mbps, 1),
            )
            last_update = now

    elapsed = max(time.time() - start, 1e-6)
    upload_mbps = round((sent * 8) / (elapsed * 1_000_000), 1)

    resp = conn.getresponse()
    resp.read()
    conn.close()
    return upload_mbps


def _run_speedtest():
    try:
        _speed_update(phase="ping", progress=0.0, current_mbps=0.0, error=None)
        ping_ms, jitter_ms = _measure_ping()
        _speed_update(ping_ms=ping_ms, jitter_ms=jitter_ms)

        _speed_update(phase="download", progress=0.0, current_mbps=0.0)
        download_mbps = _measure_download(SPEEDTEST_DOWNLOAD_BYTES)
        _speed_update(download_mbps=download_mbps, progress=100.0)

        _speed_update(phase="upload", progress=0.0, current_mbps=0.0)
        upload_mbps = _measure_upload(SPEEDTEST_UPLOAD_BYTES)
        _speed_update(upload_mbps=upload_mbps, progress=100.0)

        _speed_update(status="done", phase=None, current_mbps=0.0, timestamp=time.time())
    except Exception as e:
        _speed_update(status="error", error=str(e), phase=None, current_mbps=0.0)


def start_speedtest():
    with _speed_lock:
        if _speed_state["status"] == "testing":
            return False
        _speed_state.update({
            "status": "testing",
            "phase": None,
            "progress": 0.0,
            "current_mbps": 0.0,
            "ping_ms": None,
            "jitter_ms": None,
            "download_mbps": None,
            "upload_mbps": None,
            "error": None,
        })
    threading.Thread(target=_run_speedtest, daemon=True).start()
    return True


HTML_PAGE = """<!DOCTYPE html>
<html lang="pt-br">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Monitor de Recursos</title>
<style>
  :root {
    --bg: #0b0e14;
    --panel: #131722;
    --border: #232838;
    --text: #e6e9f0;
    --muted: #7b849e;
    --cpu: #4fc3f7;
    --mem: #ba68c8;
    --disk: #ffb74d;
    --net-up: #ff5c8a;
    --net-down: #66bb6a;
    --dl: #4fc3f7;
    --ul: #ff5c8a;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background: var(--bg);
    color: var(--text);
    font-family: "Segoe UI", Consolas, monospace;
    padding: 24px;
  }
  h1 { font-size: 20px; font-weight: 600; margin: 0 0 4px; }
  .subtitle { color: var(--muted); font-size: 13px; margin-bottom: 20px; }
  .grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 16px;
  }
  .card {
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 16px;
  }
  .card h2 {
    font-size: 13px;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    color: var(--muted);
    margin: 0 0 12px;
    display: flex;
    justify-content: space-between;
  }
  .big-value { font-size: 28px; font-weight: 700; }
  .row { display: flex; justify-content: space-between; font-size: 12px; color: var(--muted); margin-top: 4px; }
  canvas { width: 100%; height: 80px; display: block; margin-top: 10px; }
  .cores { display: grid; grid-template-columns: repeat(auto-fill, minmax(50px, 1fr)); gap: 6px; margin-top: 10px; }
  .core { background: #1a2030; border-radius: 4px; padding: 4px 2px; text-align: center; font-size: 11px; }
  .core-bar { height: 4px; background: #232838; border-radius: 2px; margin-top: 3px; overflow: hidden; }
  .core-bar-fill { height: 100%; background: var(--cpu); }
  table { width: 100%; border-collapse: collapse; font-size: 12px; margin-top: 8px; }
  th, td { text-align: left; padding: 4px 2px; border-bottom: 1px solid var(--border); }
  th { color: var(--muted); font-weight: 500; }
  .bar-bg { background: #1a2030; border-radius: 4px; height: 8px; overflow: hidden; margin-top: 8px; }
  .bar-fill { height: 100%; border-radius: 4px; }
  .status { color: var(--muted); font-size: 12px; }
  .dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #66bb6a; margin-right: 6px; }
  .speed-header { display: flex; justify-content: space-between; align-items: center; }
  .btn {
    background: #1a2030;
    border: 1px solid var(--border);
    color: var(--text);
    padding: 8px 16px;
    border-radius: 6px;
    font-size: 13px;
    cursor: pointer;
  }
  .btn:hover { background: #232a3d; }
  .btn:disabled { opacity: 0.5; cursor: default; }
  .speed-live { text-align: center; padding: 20px 0 8px; }
  .speed-live .phase { color: var(--muted); font-size: 13px; text-transform: uppercase; letter-spacing: 0.08em; }
  .speed-live .value { font-size: 42px; font-weight: 700; margin-top: 4px; }
  .speed-live .unit { font-size: 16px; color: var(--muted); font-weight: 400; }
  .speed-bar-bg { background: #1a2030; border-radius: 4px; height: 6px; overflow: hidden; margin: 12px 0 20px; }
  .speed-bar-fill { height: 100%; border-radius: 4px; background: var(--dl); transition: width 0.15s linear; }
  .speed-results { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 12px; }
  .speed-result { background: #1a2030; border-radius: 8px; padding: 10px; text-align: center; }
  .speed-result .label { color: var(--muted); font-size: 11px; text-transform: uppercase; letter-spacing: 0.06em; }
  .speed-result .val { font-size: 20px; font-weight: 700; margin-top: 4px; }
  .speed-meta { color: var(--muted); font-size: 12px; margin-top: 10px; text-align: right; }
</style>
</head>
<body>
  <h1>Monitor de Recursos da Maquina</h1>
  <div class="subtitle"><span class="dot"></span><span id="status">conectando...</span> - atualiza a cada 1s</div>

  <div class="grid">
    <div class="card">
      <h2>CPU <span id="cpu-freq"></span></h2>
      <div class="big-value" id="cpu-percent">--%</div>
      <div class="row"><span id="cpu-loadavg"></span></div>
      <canvas id="cpu-chart"></canvas>
      <div class="cores" id="cpu-cores"></div>
    </div>

    <div class="card">
      <h2>Memoria</h2>
      <div class="big-value" id="mem-percent">--%</div>
      <div class="row"><span id="mem-used"></span><span id="mem-total"></span></div>
      <canvas id="mem-chart"></canvas>
    </div>

    <div class="card">
      <h2>Disco</h2>
      <div class="big-value" id="disk-percent">--%</div>
      <div class="row"><span id="disk-used"></span><span id="disk-total"></span></div>
      <div class="bar-bg"><div class="bar-fill" id="disk-bar" style="background:var(--disk); width:0%"></div></div>
      <div class="row" style="margin-top:10px">
        <span>Leitura: <b id="disk-read">0 B/s</b></span>
        <span>Escrita: <b id="disk-write">0 B/s</b></span>
      </div>
    </div>

    <div class="card">
      <h2>Rede</h2>
      <div class="row" style="font-size:13px">
        <span style="color:var(--net-down)">&#8595; Download: <b id="net-down">0 B/s</b></span>
        <span style="color:var(--net-up)">&#8593; Upload: <b id="net-up">0 B/s</b></span>
      </div>
      <canvas id="net-chart"></canvas>
      <div class="row" style="margin-top:6px">
        <span>Total recebido: <b id="net-total-recv"></b></span>
        <span>Total enviado: <b id="net-total-sent"></b></span>
      </div>
    </div>

    <div class="card" style="grid-column: 1 / -1;">
      <div class="speed-header">
        <h2 style="margin:0">Velocidade da Internet</h2>
        <button class="btn" id="speed-btn">Iniciar Teste</button>
      </div>

      <div class="speed-live">
        <div class="phase" id="speed-phase">Pronto para testar</div>
        <div class="value"><span id="speed-live-value">0.0</span><span class="unit"> Mbps</span></div>
      </div>
      <div class="speed-bar-bg"><div class="speed-bar-fill" id="speed-bar" style="width:0%"></div></div>

      <div class="speed-results">
        <div class="speed-result"><div class="label">Ping</div><div class="val" id="speed-ping">--</div></div>
        <div class="speed-result"><div class="label">Jitter</div><div class="val" id="speed-jitter">--</div></div>
        <div class="speed-result"><div class="label">Download</div><div class="val" id="speed-download">--</div></div>
        <div class="speed-result"><div class="label">Upload</div><div class="val" id="speed-upload">--</div></div>
      </div>
      <div class="speed-meta" id="speed-meta"></div>
    </div>

    <div class="card" style="grid-column: 1 / -1;">
      <h2>Top Processos (CPU)</h2>
      <table>
        <thead><tr><th>PID</th><th>Nome</th><th>CPU %</th><th>Mem %</th></tr></thead>
        <tbody id="proc-table"></tbody>
      </table>
    </div>
  </div>

<script>
const MAX_POINTS = 60;
const history = { cpu: [], mem: [], netUp: [], netDown: [] };

function fmtBytes(n) {
  if (n === undefined || n === null) return "-";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let i = 0;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
  return n.toFixed(1) + " " + units[i];
}

function fmtRate(n) { return fmtBytes(n) + "/s"; }

function drawChart(canvas, series, color, maxHint) {
  const ctx = canvas.getContext("2d");
  const dpr = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  canvas.width = w * dpr;
  canvas.height = h * dpr;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, w, h);
  if (series.length < 2) return;

  const max = Math.max(maxHint || 0, ...series, 1);
  ctx.beginPath();
  series.forEach((v, i) => {
    const x = (i / (MAX_POINTS - 1)) * w;
    const y = h - (v / max) * h;
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  });
  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.stroke();

  ctx.lineTo(w, h);
  ctx.lineTo(0, h);
  ctx.closePath();
  ctx.fillStyle = color + "22";
  ctx.fill();
}

function pushHistory(arr, val) {
  arr.push(val);
  if (arr.length > MAX_POINTS) arr.shift();
}

async function tick() {
  try {
    const res = await fetch("/api/stats");
    const d = await res.json();

    document.getElementById("status").textContent = "conectado";

    // CPU
    document.getElementById("cpu-percent").textContent = d.cpu.percent.toFixed(1) + "%";
    document.getElementById("cpu-freq").textContent = d.cpu.freq_mhz ? (d.cpu.freq_mhz + " MHz") : "";
    document.getElementById("cpu-loadavg").textContent = d.cpu.load_avg
      ? `Load avg: ${d.cpu.load_avg["1min"]} / ${d.cpu.load_avg["5min"]} / ${d.cpu.load_avg["15min"]}`
      : "";
    pushHistory(history.cpu, d.cpu.percent);
    drawChart(document.getElementById("cpu-chart"), history.cpu, "#4fc3f7", 100);

    const coresEl = document.getElementById("cpu-cores");
    coresEl.innerHTML = d.cpu.per_core.map((v, i) =>
      `<div class="core">C${i}<div class="core-bar"><div class="core-bar-fill" style="width:${v}%"></div></div></div>`
    ).join("");

    // Memoria
    document.getElementById("mem-percent").textContent = d.memory.percent.toFixed(1) + "%";
    document.getElementById("mem-used").textContent = "Usado: " + fmtBytes(d.memory.used);
    document.getElementById("mem-total").textContent = "Total: " + fmtBytes(d.memory.total);
    pushHistory(history.mem, d.memory.percent);
    drawChart(document.getElementById("mem-chart"), history.mem, "#ba68c8", 100);

    // Disco
    document.getElementById("disk-percent").textContent = d.disk.percent.toFixed(1) + "%";
    document.getElementById("disk-used").textContent = "Usado: " + fmtBytes(d.disk.used);
    document.getElementById("disk-total").textContent = "Total: " + fmtBytes(d.disk.total);
    document.getElementById("disk-bar").style.width = d.disk.percent + "%";
    document.getElementById("disk-read").textContent = fmtRate(d.disk.read_rate);
    document.getElementById("disk-write").textContent = fmtRate(d.disk.write_rate);

    // Rede
    document.getElementById("net-down").textContent = fmtRate(d.network.download_rate);
    document.getElementById("net-up").textContent = fmtRate(d.network.upload_rate);
    document.getElementById("net-total-recv").textContent = fmtBytes(d.network.total_recv);
    document.getElementById("net-total-sent").textContent = fmtBytes(d.network.total_sent);
    pushHistory(history.netDown, d.network.download_rate);
    pushHistory(history.netUp, d.network.upload_rate);
    const netCanvas = document.getElementById("net-chart");
    const ctx = netCanvas.getContext("2d");
    drawChart(netCanvas, history.netDown, "#66bb6a");
    // overlay upload line
    const dpr = window.devicePixelRatio || 1;
    const w = netCanvas.clientWidth, h = netCanvas.clientHeight;
    const max = Math.max(...history.netDown, ...history.netUp, 1);
    if (history.netUp.length >= 2) {
      ctx.beginPath();
      history.netUp.forEach((v, i) => {
        const x = (i / (MAX_POINTS - 1)) * w;
        const y = h - (v / max) * h;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      });
      ctx.strokeStyle = "#ff5c8a";
      ctx.lineWidth = 2;
      ctx.stroke();
    }

    // Processos
    const tbody = document.getElementById("proc-table");
    tbody.innerHTML = d.processes.map(p =>
      `<tr><td>${p.pid}</td><td>${p.name}</td><td>${p.cpu.toFixed(1)}%</td><td>${p.mem.toFixed(1)}%</td></tr>`
    ).join("");
  } catch (e) {
    document.getElementById("status").textContent = "desconectado - tentando reconectar...";
  }
}

tick();
setInterval(tick, 1000);

// --- Teste de velocidade ---
const speedBtn = document.getElementById("speed-btn");
const speedPhaseEl = document.getElementById("speed-phase");
const speedLiveEl = document.getElementById("speed-live-value");
const speedBarEl = document.getElementById("speed-bar");
const speedMetaEl = document.getElementById("speed-meta");

const PHASE_LABELS = { ping: "Medindo ping...", download: "Testando download...", upload: "Testando upload..." };
const PHASE_COLORS = { download: "var(--dl)", upload: "var(--ul)" };

function renderSpeedResults(d) {
  document.getElementById("speed-ping").textContent = d.ping_ms !== null ? d.ping_ms + " ms" : "--";
  document.getElementById("speed-jitter").textContent = d.jitter_ms !== null ? d.jitter_ms + " ms" : "--";
  document.getElementById("speed-download").textContent = d.download_mbps !== null ? d.download_mbps + " Mbps" : "--";
  document.getElementById("speed-upload").textContent = d.upload_mbps !== null ? d.upload_mbps + " Mbps" : "--";
}

async function pollSpeed() {
  try {
    const res = await fetch("/api/speedtest/status");
    const d = await res.json();
    renderSpeedResults(d);

    if (d.status === "testing") {
      speedPhaseEl.textContent = PHASE_LABELS[d.phase] || "Testando...";
      speedLiveEl.textContent = d.current_mbps.toFixed(1);
      speedBarEl.style.width = d.progress + "%";
      speedBarEl.style.background = PHASE_COLORS[d.phase] || "var(--dl)";
      setTimeout(pollSpeed, 300);
    } else if (d.status === "done") {
      speedPhaseEl.textContent = "Teste concluido";
      speedLiveEl.textContent = "0.0";
      speedBarEl.style.width = "100%";
      speedMetaEl.textContent = "Ultimo teste: " + new Date(d.timestamp * 1000).toLocaleTimeString();
      speedBtn.disabled = false;
    } else if (d.status === "error") {
      speedPhaseEl.textContent = "Erro: " + (d.error || "falha desconhecida");
      speedLiveEl.textContent = "0.0";
      speedBarEl.style.width = "0%";
      speedBtn.disabled = false;
    }
  } catch (e) {
    speedPhaseEl.textContent = "Erro ao consultar o teste";
    speedBtn.disabled = false;
  }
}

speedBtn.addEventListener("click", async () => {
  speedBtn.disabled = true;
  document.getElementById("speed-ping").textContent = "--";
  document.getElementById("speed-jitter").textContent = "--";
  document.getElementById("speed-download").textContent = "--";
  document.getElementById("speed-upload").textContent = "--";
  speedBarEl.style.width = "0%";
  try {
    const res = await fetch("/api/speedtest/start", { method: "POST" });
    const d = await res.json();
    if (!d.started) {
      speedBtn.disabled = false;
      return;
    }
    pollSpeed();
  } catch (e) {
    speedPhaseEl.textContent = "Nao foi possivel iniciar o teste";
    speedBtn.disabled = false;
  }
});
</script>
</body>
</html>
"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass  # silencia log padrao no console

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            body = HTML_PAGE.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif self.path == "/api/stats":
            body = json.dumps(get_stats()).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif self.path == "/api/speedtest/status":
            body = json.dumps(_speed_snapshot()).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(404)
            self.end_headers()

    def do_POST(self):
        if self.path == "/api/speedtest/start":
            started = start_speedtest()
            body = json.dumps({"started": started}).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(404)
            self.end_headers()


def main():
    server = ThreadingHTTPServer((HOST, PORT), Handler)
    print(f"Painel de monitoramento rodando em http://{HOST}:{PORT}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nEncerrando servidor...")
        server.shutdown()


if __name__ == "__main__":
    main()
