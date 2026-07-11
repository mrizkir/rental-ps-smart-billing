import logging
from typing import Optional

from samsungtvws import SamsungTVWS
from wakeonlan import send_magic_packet

from config import TV_WS_PORT

logger = logging.getLogger(__name__)


class SamsungTVController:
    def __init__(
        self,
        tv_ip: str,
        tv_mac: str,
        token: Optional[str] = None,
        ws_port: Optional[int] = None,
    ):
        self.tv_ip = tv_ip
        self.tv_mac = tv_mac
        self.token = token.strip() if token and token.strip() else None
        self.port = ws_port if ws_port is not None else TV_WS_PORT

    def _connect(self) -> SamsungTVWS:
        return SamsungTVWS(
            host=self.tv_ip,
            port=self.port,
            token=self.token,
            name="RentalPS-SmartBilling",
            timeout=10,
        )

    def _current_token(self, tv: SamsungTVWS) -> Optional[str]:
        token = getattr(tv, "token", None) or self.token
        if token is None:
            return None
        token = str(token).strip()
        return token or None

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
            result = {"success": True, "message": "TV power off command sent"}
            token = self._current_token(tv)
            if token:
                result["token"] = token
            return result
        except Exception as exc:
            logger.exception("Failed to power off TV %s", self.tv_ip)
            return {"success": False, "message": f"Power off failed: {exc}"}

    def set_hdmi(self) -> dict:
        try:
            tv = self._connect()
            tv.send_key("KEY_HDMI1")
            logger.info("Switched TV %s to HDMI1", self.tv_ip)
            result = {"success": True, "message": "TV switched to HDMI1"}
            token = self._current_token(tv)
            if token:
                result["token"] = token
            return result
        except Exception as exc:
            logger.exception("Failed to set HDMI on TV %s", self.tv_ip)
            return {"success": False, "message": f"Set HDMI failed: {exc}"}

    def send_key(self, key: str) -> dict:
        try:
            tv = self._connect()
            tv.send_key(key)
            logger.info("Sent %s to TV %s", key, self.tv_ip)
            result = {"success": True, "message": f"Key {key} sent to TV"}
            token = self._current_token(tv)
            if token:
                result["token"] = token
            return result
        except Exception as exc:
            logger.exception("Failed to send key %s to TV %s", key, self.tv_ip)
            return {"success": False, "message": f"Send key failed: {exc}"}

    def open_browser(self, url: str) -> dict:
        try:
            tv = self._connect()
            tv.open_browser(url)
            logger.info("Opened browser on TV %s: %s", self.tv_ip, url)
            result = {"success": True, "message": f"Browser opened with URL: {url}"}
            token = self._current_token(tv)
            if token:
                result["token"] = token
            return result
        except Exception as exc:
            logger.exception("Failed to open browser on TV %s", self.tv_ip)
            return {"success": False, "message": f"Open browser failed: {exc}"}

    def get_status(self) -> dict:
        try:
            tv = self._connect()
            info = tv.rest_device_info()

            # Buka remote WS agar pairing/token ter-refresh jika perlu
            try:
                tv.open()
                tv.close()
            except Exception as ws_exc:
                logger.debug("TV %s websocket open skipped/failed: %s", self.tv_ip, ws_exc)

            logger.info("TV %s is reachable", self.tv_ip)
            result = {
                "success": True,
                "message": "TV connected",
                "data": info,
            }
            token = self._current_token(tv)
            if token:
                result["token"] = token
                self.token = token
            return result
        except Exception as exc:
            logger.warning("TV %s not reachable: %s", self.tv_ip, exc)
            return {"success": False, "message": f"TV not reachable: {exc}"}
