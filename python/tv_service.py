import logging
from typing import Optional

from flask import Flask, jsonify, request
from flask_cors import CORS

from config import (
    FLASK_HOST,
    FLASK_PORT,
    TV_WS_PORT,
)
from ps4_controller import PS4Controller
from splash_server import SplashServer
from tv_controller import SamsungTVController
from tv_notification_store import consume_notification, set_notification

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
