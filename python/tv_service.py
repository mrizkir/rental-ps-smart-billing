import logging

from flask import Flask, jsonify, request
from flask_cors import CORS

from config import (
    DEFAULT_PS4_MAC,
    DEFAULT_TV_IP,
    DEFAULT_TV_MAC,
    FLASK_HOST,
    FLASK_PORT,
    TV_WS_PORT,
)
from ps4_controller import PS4Controller
from splash_server import SplashServer
from tv_controller import SamsungTVController

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


def get_device_config() -> dict:
    data = request.get_json(silent=True) or {}
    if request.method == "GET":
        data = {**request.args.to_dict(), **data}

    raw_token = data.get("tv_token")
    if raw_token is None or raw_token == "":
        token = None
    else:
        token = str(raw_token).strip() or None

    return {
        "tv_ip": data.get("tv_ip", DEFAULT_TV_IP),
        "tv_mac": data.get("tv_mac", DEFAULT_TV_MAC),
        "tv_token": token,
        "ps4_mac": data.get("ps4_mac", DEFAULT_PS4_MAC),
        "ws_port": _parse_ws_port(data.get("ws_port")),
    }


def get_tv_controller() -> SamsungTVController:
    config = get_device_config()
    return SamsungTVController(
        tv_ip=config["tv_ip"],
        tv_mac=config["tv_mac"],
        token=config["tv_token"],
        ws_port=config["ws_port"],
    )


@app.route("/health", methods=["GET"])
def health():
    logger.info("Health check requested")
    return jsonify({"success": True, "message": "TV service is running"})


@app.route("/tv/power-on", methods=["POST"])
def tv_power_on():
    logger.info("TV power-on requested")
    result = get_tv_controller().power_on()
    return jsonify(result), 200 if result["success"] else 500


@app.route("/tv/power-off", methods=["POST"])
def tv_power_off():
    logger.info("TV power-off requested")
    result = get_tv_controller().power_off()
    return jsonify(result), 200 if result["success"] else 500


@app.route("/tv/set-hdmi", methods=["POST"])
def tv_set_hdmi():
    logger.info("TV set-hdmi requested")
    result = get_tv_controller().set_hdmi()
    return jsonify(result), 200 if result["success"] else 500


@app.route("/tv/send-key", methods=["POST"])
def tv_send_key():
    data = request.get_json(silent=True) or {}
    key = data.get("key")
    if not key:
        return jsonify({"success": False, "message": "Missing required field: key"}), 400

    logger.info("TV send-key requested: %s", key)
    result = get_tv_controller().send_key(key)
    return jsonify(result), 200 if result["success"] else 500


@app.route("/tv/open-browser", methods=["POST"])
def tv_open_browser():
    data = request.get_json(silent=True) or {}
    url = data.get("url")
    if not url:
        return jsonify({"success": False, "message": "Missing required field: url"}), 400

    logger.info("TV open-browser requested: %s", url)
    result = get_tv_controller().open_browser(url)
    return jsonify(result), 200 if result["success"] else 500


@app.route("/tv/status", methods=["GET"])
def tv_status():
    logger.info("TV status requested")
    result = get_tv_controller().get_status()
    return jsonify(result), 200 if result["success"] else 503


@app.route("/ps4/power-on", methods=["POST"])
def ps4_power_on():
    config = get_device_config()
    logger.info("PS4 power-on requested")
    result = ps4_controller.power_on(config["ps4_mac"])
    return jsonify(result), 200 if result["success"] else 500


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
    except Exception as exc:
        logger.exception("Failed to show splash screen")
        return jsonify({"success": False, "message": f"Splash show failed: {exc}"}), 500


if __name__ == "__main__":
    logger.info("Starting TV service on http://%s:%s", FLASK_HOST, FLASK_PORT)
    app.run(host=FLASK_HOST, port=FLASK_PORT, debug=False, threaded=True)
