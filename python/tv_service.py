import logging
from typing import Optional

from flask import Flask, jsonify, request
from flask_cors import CORS

from config import (
    FLASK_HOST,
    FLASK_PORT,
    SPLASH_PORT,
    SPLASH_PUBLIC_IP,
    TV_WS_PORT,
)
from ps4_controller import PS4Controller
from splash_server import SplashServer
from tv_controller import SamsungTVController
from tv_notification_store import consume_notification, set_notification
from tv_session_overlay_store import get_session_overlay, set_session_overlay

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)

app = Flask(__name__)
CORS(app)

splash_server = SplashServer()
ps4_controller = PS4Controller()


def _parse_ws_port(value) -> int:
    if value is None or value == "":
        return TV_WS_PORT
    try:
        port = int(value)
        if 1 <= port <= 65535:
            return port
    except (TypeError, ValueError):
        pass
    return TV_WS_PORT


def _optional_str(value) -> Optional[str]:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def get_device_config() -> dict:
    data = request.get_json(silent=True) or {}
    if request.method == "GET":
        data = {**request.args.to_dict(), **data}

    return {
        "tv_ip": _optional_str(data.get("tv_ip")),
        "tv_mac": _optional_str(data.get("tv_mac")),
        "tv_token": _optional_str(data.get("tv_token")),
        "ps4_mac": _optional_str(data.get("ps4_mac")),
        "ws_port": _parse_ws_port(data.get("ws_port")),
    }


def get_tv_controller() -> SamsungTVController:
    config = get_device_config()
    missing = [name for name in ("tv_ip", "tv_mac") if not config[name]]
    if missing:
        raise ValueError(f"Missing required field(s): {', '.join(missing)}")

    return SamsungTVController(
        tv_ip=config["tv_ip"],
        tv_mac=config["tv_mac"],
        token=config["tv_token"],
        ws_port=config["ws_port"],
    )


def _tv_action(action, *, failure_status: int = 500):
    try:
        result = action(get_tv_controller())
    except ValueError as exc:
        return jsonify({"success": False, "message": str(exc)}), 400
    return jsonify(result), 200 if result["success"] else failure_status


@app.route("/", methods=["GET"])
def index():
    """Halaman keterangan saat dibuka di browser (bukan 404 kosong)."""
    return f"""<!DOCTYPE html>
<html lang="id">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>TV Service — Rental PS Smart Billing</title>
  <style>
    :root {{
      --bg: #eceff1;
      --card: #ffffff;
      --ink: #37474f;
      --muted: #78909c;
      --accent: #1a237e;
      --ok: #2e7d32;
      --border: #cfd8dc;
    }}
    * {{ box-sizing: border-box; }}
    body {{
      margin: 0;
      min-height: 100vh;
      font-family: "Segoe UI", system-ui, sans-serif;
      background: linear-gradient(160deg, #eceff1 0%, #cfd8dc 100%);
      color: var(--ink);
      padding: 32px 20px;
    }}
    main {{
      max-width: 640px;
      margin: 0 auto;
      background: var(--card);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 28px 28px 24px;
      box-shadow: 0 8px 24px rgba(55, 71, 79, 0.08);
    }}
    h1 {{
      margin: 0 0 6px;
      font-size: 1.45rem;
      color: var(--accent);
    }}
    .status {{
      display: inline-flex;
      align-items: center;
      gap: 8px;
      margin: 12px 0 20px;
      padding: 6px 12px;
      border-radius: 999px;
      background: #e8f5e9;
      color: var(--ok);
      font-weight: 600;
      font-size: 0.9rem;
    }}
    .status::before {{
      content: "";
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--ok);
    }}
    p {{ margin: 0 0 14px; line-height: 1.5; color: var(--ink); }}
    .muted {{ color: var(--muted); font-size: 0.92rem; }}
    h2 {{
      margin: 22px 0 10px;
      font-size: 1rem;
      color: var(--accent);
    }}
    ul {{
      margin: 0;
      padding-left: 1.2rem;
      line-height: 1.7;
    }}
    code {{
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 0.88em;
      background: #f5f7f8;
      padding: 1px 6px;
      border-radius: 4px;
      border: 1px solid var(--border);
    }}
    .meta {{
      margin-top: 24px;
      padding-top: 16px;
      border-top: 1px solid var(--border);
      font-size: 0.88rem;
      color: var(--muted);
    }}
    a {{ color: #1565c0; }}
  </style>
</head>
<body>
  <main>
    <h1>TV Service</h1>
    <p class="muted">Rental PS Smart Billing — layanan kontrol Smart TV / PS</p>
    <div class="status">Berjalan</div>
    <p>
      Ini API backend untuk aplikasi kasir desktop dan overlay Tizen di TV.
      Buka endpoint di bawah, jangan mengandalkan halaman ini untuk kontrol TV.
    </p>

    <h2>Endpoint utama</h2>
    <ul>
      <li><code>GET /health</code> — cek layanan hidup</li>
      <li><code>POST /tv/power-on</code> / <code>/tv/power-off</code> — nyala / mati TV</li>
      <li><code>POST /tv/set-hdmi</code> — pindah HDMI</li>
      <li><code>POST /tv/send-key</code> — kirim remote key</li>
      <li><code>POST /tv/open-browser</code> — buka URL di browser TV</li>
      <li><code>GET /tv/status</code> — status koneksi TV</li>
      <li><code>POST /ps4/power-on</code> — Wake-on-LAN PS4</li>
      <li><code>GET/POST /api/tv-notification</code> — banner peringatan ke overlay Tizen</li>
      <li><code>GET/POST /api/tv-session</code> — HUD paket + countdown di Tizen</li>
      <li><code>POST /splash/show</code> — tampilkan splash sewa di TV</li>
    </ul>

    <h2>Konfigurasi saat ini</h2>
    <ul>
      <li>Host: <code>{FLASK_HOST}</code></li>
      <li>Port: <code>{FLASK_PORT}</code></li>
      <li>Splash public IP: <code>{SPLASH_PUBLIC_IP}</code>:{SPLASH_PORT}</li>
      <li>TV WebSocket default port: <code>{TV_WS_PORT}</code></li>
    </ul>

    <p class="meta">
      Cek cepat: <a href="/health">/health</a>
    </p>
  </main>
</body>
</html>
""", 200, {"Content-Type": "text/html; charset=utf-8"}


@app.route("/health", methods=["GET"])
def health():
    logger.info("Health check requested")
    return jsonify({"success": True, "message": "TV service is running"})


@app.route("/tv/power-on", methods=["POST"])
def tv_power_on():
    logger.info("TV power-on requested")
    return _tv_action(lambda tv: tv.power_on())


@app.route("/tv/power-off", methods=["POST"])
def tv_power_off():
    logger.info("TV power-off requested")
    return _tv_action(lambda tv: tv.power_off())


@app.route("/tv/set-hdmi", methods=["POST"])
def tv_set_hdmi():
    logger.info("TV set-hdmi requested")
    return _tv_action(lambda tv: tv.set_hdmi())


@app.route("/tv/send-key", methods=["POST"])
def tv_send_key():
    data = request.get_json(silent=True) or {}
    key = data.get("key")
    if not key:
        return jsonify({"success": False, "message": "Missing required field: key"}), 400

    logger.info("TV send-key requested: %s", key)
    return _tv_action(lambda tv: tv.send_key(key))


@app.route("/tv/open-browser", methods=["POST"])
def tv_open_browser():
    data = request.get_json(silent=True) or {}
    url = data.get("url")
    if not url:
        return jsonify({"success": False, "message": "Missing required field: url"}), 400

    logger.info("TV open-browser requested: %s", url)
    return _tv_action(lambda tv: tv.open_browser(url))


@app.route("/tv/status", methods=["GET"])
def tv_status():
    logger.info("TV status requested")
    return _tv_action(lambda tv: tv.get_status(), failure_status=503)


@app.route("/ps4/power-on", methods=["POST"])
def ps4_power_on():
    config = get_device_config()
    if not config["ps4_mac"]:
        return jsonify(
            {"success": False, "message": "Missing required field: ps4_mac"}
        ), 400

    logger.info("PS4 power-on requested")
    result = ps4_controller.power_on(config["ps4_mac"])
    return jsonify(result), 200 if result["success"] else 500


@app.route("/api/tv-notification", methods=["GET"])
def tv_notification_get():
    """Polled by Tizen overlay app. Consumes warning so banner shows once."""
    tv_id = request.args.get("tv_id")
    payload = consume_notification(tv_id)
    logger.info(
        "TV notification GET tv_id=%s show_warning=%s",
        tv_id or "default",
        payload["show_warning"],
    )
    return jsonify(payload)


@app.route("/api/tv-notification", methods=["POST"])
def tv_notification_post():
    """Set by desktop billing app when a session is about to end."""
    data = request.get_json(silent=True) or {}
    tv_id = data.get("tv_id")
    show_warning = bool(data.get("show_warning", False))
    message = data.get("message") or ""

    if show_warning and not str(message).strip():
        message = "Waktu hampir habis"

    payload = set_notification(tv_id, show_warning, str(message))
    logger.info(
        "TV notification POST tv_id=%s show_warning=%s message=%s",
        tv_id if tv_id is not None else "default",
        payload["show_warning"],
        payload["message"],
    )
    return jsonify({"success": True, **payload})


@app.route("/api/tv-session", methods=["GET"])
def tv_session_get():
    """Polled by Tizen HUD. Does not consume — countdown needs continuous state."""
    tv_id = request.args.get("tv_id")
    payload = get_session_overlay(tv_id)
    return jsonify(payload)


@app.route("/api/tv-session", methods=["POST"])
def tv_session_post():
    """Set/clear by desktop billing app when session starts, extends, or ends."""
    data = request.get_json(silent=True) or {}
    tv_id = data.get("tv_id")
    active = bool(data.get("active", False))
    package_name = data.get("package_name") or ""
    customer_name = data.get("customer_name") or ""
    billing_mode = data.get("billing_mode") or "Fixed"
    ends_at = data.get("ends_at")

    payload = set_session_overlay(
        tv_id,
        active=active,
        package_name=str(package_name),
        customer_name=str(customer_name),
        billing_mode=str(billing_mode),
        ends_at=str(ends_at) if ends_at else None,
    )
    logger.info(
        "TV session overlay POST tv_id=%s active=%s package=%s ends_at=%s mode=%s",
        tv_id if tv_id is not None else "default",
        payload["active"],
        payload.get("package_name") or "",
        payload.get("ends_at"),
        payload.get("billing_mode"),
    )
    return jsonify({"success": True, **payload})


@app.route("/splash/show", methods=["POST"])
def splash_show():
    data = request.get_json(silent=True) or {}
    unit = data.get("unit")
    durasi = data.get("durasi")
    nama = data.get("nama")

    if unit is None or not durasi or not nama:
        return jsonify(
            {
                "success": False,
                "message": "Missing required fields: unit, durasi, nama",
            }
        ), 400

    logger.info("Splash show requested: unit=%s, durasi=%s, nama=%s", unit, durasi, nama)

    try:
        splash_server.start()
        url = splash_server.get_url(unit=unit, durasi=durasi, nama=nama)
        result = get_tv_controller().open_browser(url)

        if result["success"]:
            result["url"] = url

        return jsonify(result), 200 if result["success"] else 500
    except ValueError as exc:
        return jsonify({"success": False, "message": str(exc)}), 400
    except Exception as exc:
        logger.exception("Failed to show splash screen")
        return jsonify({"success": False, "message": f"Splash show failed: {exc}"}), 500


if __name__ == "__main__":
    logger.info("Starting TV service on http://%s:%s", FLASK_HOST, FLASK_PORT)
    app.run(host=FLASK_HOST, port=FLASK_PORT, debug=False, threaded=True)
