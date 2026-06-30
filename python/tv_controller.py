import logging
import os

from samsungtvws import SamsungTVWS
from wakeonlan import send_magic_packet

from config import BASE_DIR, TV_WS_PORT

logger = logging.getLogger(__name__)


def resolve_token_file(token_file: str) -> str:
    if os.path.isabs(token_file):
        return token_file
    return os.path.join(BASE_DIR, token_file)


class SamsungTVController:
    def __init__(self, tv_ip: str, tv_mac: str, token_file: str):
        self.tv_ip = tv_ip
        self.tv_mac = tv_mac
        self.token_file = resolve_token_file(token_file)
        self.port = TV_WS_PORT

    def _connect(self) -> SamsungTVWS:
        return SamsungTVWS(
            host=self.tv_ip,
            port=self.port,
            token_file=self.token_file,
            name="RentalPS-SmartBilling",
            timeout=10,
        )

    def power_on(self) -> dict:
        try:
            logger.info("Sending Wake-on-LAN to TV %s", self.tv_mac)
            send_magic_packet(self.tv_mac)
            return {"success": True, "message": "Wake-on-LAN packet sent to TV"}
        except Exception as exc:
            logger.exception("Failed to send Wake-on-LAN to TV %s", self.tv_mac)
            return {"success": False, "message": f"Wake-on-LAN failed: {exc}"}

    def power_off(self) -> dict:
        try:
            tv = self._connect()
            tv.send_key("KEY_POWER")
            logger.info("Sent KEY_POWER to TV %s", self.tv_ip)
            return {"success": True, "message": "TV power off command sent"}
        except Exception as exc:
            logger.exception("Failed to power off TV %s", self.tv_ip)
            return {"success": False, "message": f"Power off failed: {exc}"}

    def set_hdmi(self) -> dict:
        try:
            tv = self._connect()
            tv.send_key("KEY_HDMI1")
            logger.info("Switched TV %s to HDMI1", self.tv_ip)
            return {"success": True, "message": "TV switched to HDMI1"}
        except Exception as exc:
            logger.exception("Failed to set HDMI on TV %s", self.tv_ip)
            return {"success": False, "message": f"Set HDMI failed: {exc}"}

    def send_key(self, key: str) -> dict:
        try:
            tv = self._connect()
            tv.send_key(key)
            logger.info("Sent %s to TV %s", key, self.tv_ip)
            return {"success": True, "message": f"Key {key} sent to TV"}
        except Exception as exc:
            logger.exception("Failed to send key %s to TV %s", key, self.tv_ip)
            return {"success": False, "message": f"Send key failed: {exc}"}

    def open_browser(self, url: str) -> dict:
        try:
            tv = self._connect()
            tv.open_browser(url)
            logger.info("Opened browser on TV %s: %s", self.tv_ip, url)
            return {"success": True, "message": f"Browser opened with URL: {url}"}
        except Exception as exc:
            logger.exception("Failed to open browser on TV %s", self.tv_ip)
            return {"success": False, "message": f"Open browser failed: {exc}"}

    def get_status(self) -> dict:
        try:
            tv = self._connect()
            info = tv.rest_device_info()
            logger.info("TV %s is reachable", self.tv_ip)
            return {
                "success": True,
                "message": "TV connected",
                "data": info,
            }
        except Exception as exc:
            logger.warning("TV %s not reachable: %s", self.tv_ip, exc)
            return {"success": False, "message": f"TV not reachable: {exc}"}
