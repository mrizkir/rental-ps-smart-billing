import logging
import time
import warnings
from pathlib import Path
from typing import Any, Optional

import urllib3
from samsungtvws import SamsungTVWS
from wakeonlan import send_magic_packet

from config import (
    TOKENS_DIR,
    TV_WS_PORT,
    WOL_PACKET_COUNT,
    WOL_POLL_INTERVAL,
    WOL_WAIT_SECONDS,
)

# Samsung local HTTPS uses a self-signed cert; samsungtvws already sets verify=False.
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
warnings.filterwarnings("ignore", message="Unverified HTTPS request")

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
        self._token_log_emitted = False

    def _token_file_path(self) -> Path:
        safe_ip = self.tv_ip.replace(":", "_").replace("/", "_")
        return Path(TOKENS_DIR) / f"{safe_ip}.token"

    def _load_cached_token(self) -> Optional[str]:
        path = self._token_file_path()
        if not path.exists():
            return None
        cached = path.read_text(encoding="utf-8").strip()
        return cached or None

    def _save_cached_token(self, token: str) -> None:
        path = self._token_file_path()
        path.parent.mkdir(parents=True, exist_ok=True)
        # No trailing newline: keeps token URL-safe if ever used via token_file
        path.write_text(token.strip(), encoding="utf-8")

    def _resolve_token(self) -> Optional[str]:
        """Prefer token from request/SQL; fall back to on-disk cache."""
        if self.token:
            self._save_cached_token(self.token)
            if not self._token_log_emitted:
                logger.info("TV %s: using token from request", self.tv_ip)
                self._token_log_emitted = True
            return self.token

        cached = self._load_cached_token()
        if cached:
            self.token = cached
            if not self._token_log_emitted:
                logger.info("TV %s: using cached token file", self.tv_ip)
                self._token_log_emitted = True
            return cached

        if not self._token_log_emitted:
            logger.info("TV %s: no token yet (TV may show Grant Permission)", self.tv_ip)
            self._token_log_emitted = True
        return None

    def _connect(self) -> SamsungTVWS:
        # Use token= (not token_file): samsungtvws readline() keeps trailing \\n and
        # breaks the WebSocket URL with HTTP 404.
        return SamsungTVWS(
            host=self.tv_ip,
            port=self.port,
            token=self._resolve_token(),
            name="RentalPS-SmartBilling",
            timeout=10,
        )

    def _current_token(self, tv: SamsungTVWS) -> Optional[str]:
        token = getattr(tv, "token", None) or self.token
        if token is None:
            return None
        token = str(token).strip()
        if not token:
            return None
        self.token = token
        self._save_cached_token(token)
        return token

    def _result_with_token(self, tv: Optional[SamsungTVWS], message: str) -> dict:
        result = {"success": True, "message": message}
        if tv is not None:
            token = self._current_token(tv)
            if token:
                result["token"] = token
        return result

    @staticmethod
    def _power_state(info: dict[str, Any]) -> str:
        device = info.get("device") or {}
        raw = device.get("PowerState") or info.get("PowerState") or ""
        return str(raw).strip().lower()

    def _is_powered_on(self, info: dict[str, Any]) -> bool:
        state = self._power_state(info)
        if state:
            return state == "on"
        # Legacy/no PowerState: REST responding usually means the TV is awake.
        return bool(info.get("device"))

    def _is_standby(self, info: dict[str, Any]) -> bool:
        return self._power_state(info) in ("standby", "off", "sleeping")

    def _should_send_power_off(self, info: dict[str, Any]) -> bool:
        """KEY_POWER is a toggle — only send when TV is clearly on."""
        state = self._power_state(info)
        if state in ("standby", "off", "sleeping"):
            return False
        if state == "on":
            return True
        # Unknown PowerState but REST ok — assume on so rental end can still blank the screen.
        return bool(info.get("device"))

    def _fetch_device_info(self) -> tuple[Optional[SamsungTVWS], Optional[dict[str, Any]]]:
        try:
            tv = self._connect()
            info = tv.rest_device_info()
            return tv, info
        except Exception as exc:
            logger.debug("TV %s device info unavailable: %s", self.tv_ip, exc)
            return None, None

    def _wait_until_on(self, *, via: str) -> dict:
        deadline = time.monotonic() + WOL_WAIT_SECONDS
        while time.monotonic() < deadline:
            time.sleep(WOL_POLL_INTERVAL)
            tv, info = self._fetch_device_info()
            if info is not None and self._is_powered_on(info):
                logger.info("TV %s is on (%s)", self.tv_ip, via)
                return self._result_with_token(tv, f"TV powered on via {via}")
        logger.warning(
            "TV %s did not become ready within %ss (%s)",
            self.tv_ip,
            WOL_WAIT_SECONDS,
            via,
        )
        return {
            "success": False,
            "message": (
                f"TV did not become ready within {WOL_WAIT_SECONDS}s after {via}"
            ),
        }

    def _power_on_from_standby(self, tv: SamsungTVWS, info: dict[str, Any]) -> dict:
        state = self._power_state(info) or "standby"
        logger.info(
            "TV %s is reachable but PowerState=%s; sending KEY_POWER to turn on",
            self.tv_ip,
            state,
        )
        try:
            tv.send_key("KEY_POWER")
        except Exception as exc:
            logger.exception("Failed KEY_POWER wake on TV %s", self.tv_ip)
            return {"success": False, "message": f"Power on failed: {exc}"}
        return self._wait_until_on(via="KEY_POWER from standby")

    def power_on(self) -> dict:
        try:
            tv, info = self._fetch_device_info()
            if info is not None and self._is_powered_on(info):
                logger.info("TV %s already on; skip Wake-on-LAN", self.tv_ip)
                return self._result_with_token(tv, "TV already on")

            # Network alive in standby: WOL will not help — KEY_POWER toggles to on.
            if info is not None and tv is not None:
                return self._power_on_from_standby(tv, info)

            logger.info("Sending Wake-on-LAN to TV %s", self.tv_mac)
            for _ in range(max(1, WOL_PACKET_COUNT)):
                send_magic_packet(self.tv_mac)
                time.sleep(0.3)

            key_sent = False
            deadline = time.monotonic() + WOL_WAIT_SECONDS
            while time.monotonic() < deadline:
                time.sleep(WOL_POLL_INTERVAL)
                tv, info = self._fetch_device_info()
                if info is None:
                    continue
                if self._is_powered_on(info):
                    logger.info("TV %s is on after Wake-on-LAN", self.tv_ip)
                    return self._result_with_token(tv, "TV powered on via Wake-on-LAN")
                if not key_sent and tv is not None and self._is_standby(info):
                    logger.info(
                        "TV %s woke to standby after WOL; sending KEY_POWER",
                        self.tv_ip,
                    )
                    tv.send_key("KEY_POWER")
                    key_sent = True

            logger.warning(
                "TV %s did not become ready within %ss after Wake-on-LAN",
                self.tv_ip,
                WOL_WAIT_SECONDS,
            )
            return {
                "success": False,
                "message": (
                    f"Wake-on-LAN sent, but TV did not become ready "
                    f"within {WOL_WAIT_SECONDS}s"
                ),
            }
        except Exception as exc:
            logger.exception("Failed to power on TV %s", self.tv_mac)
            return {"success": False, "message": f"Power on failed: {exc}"}

    def power_off(self) -> dict:
        try:
            tv, info = self._fetch_device_info()
            if info is None:
                logger.info(
                    "TV %s not reachable; assumed already off (skip KEY_POWER)",
                    self.tv_ip,
                )
                return {
                    "success": True,
                    "message": "TV not reachable; assumed already off",
                }

            if not self._should_send_power_off(info):
                state = self._power_state(info) or "unknown"
                logger.info(
                    "TV %s PowerState=%s; skip KEY_POWER (toggle would turn it on)",
                    self.tv_ip,
                    state,
                )
                return self._result_with_token(
                    tv,
                    f"TV already off/standby ({state}); power-off skipped",
                )

            assert tv is not None
            tv.send_key("KEY_POWER")
            logger.info("Sent KEY_POWER to TV %s", self.tv_ip)
            return self._result_with_token(tv, "TV power off command sent")
        except Exception as exc:
            logger.exception("Failed to power off TV %s", self.tv_ip)
            return {"success": False, "message": f"Power off failed: {exc}"}

    def set_hdmi(self) -> dict:
        try:
            tv = self._connect()
            tv.send_key("KEY_HDMI1")
            logger.info("Switched TV %s to HDMI1", self.tv_ip)
            return self._result_with_token(tv, "TV switched to HDMI1")
        except Exception as exc:
            logger.exception("Failed to set HDMI on TV %s", self.tv_ip)
            return {"success": False, "message": f"Set HDMI failed: {exc}"}

    def send_key(self, key: str) -> dict:
        try:
            tv = self._connect()
            tv.send_key(key)
            logger.info("Sent %s to TV %s", key, self.tv_ip)
            return self._result_with_token(tv, f"Key {key} sent to TV")
        except Exception as exc:
            logger.exception("Failed to send key %s to TV %s", key, self.tv_ip)
            return {"success": False, "message": f"Send key failed: {exc}"}

    def open_browser(self, url: str) -> dict:
        try:
            tv = self._connect()
            tv.open_browser(url)
            logger.info("Opened browser on TV %s: %s", self.tv_ip, url)
            return self._result_with_token(tv, f"Browser opened with URL: {url}")
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

            summary = self._format_device_summary(info)
            logger.info("TV %s status: %s", self.tv_ip, summary)
            result = {
                "success": True,
                "message": summary,
                "data": info,
            }
            token = self._current_token(tv)
            if token:
                result["token"] = token
            return result
        except Exception as exc:
            logger.warning("TV %s not reachable: %s", self.tv_ip, exc)
            return {"success": False, "message": f"TV not reachable: {exc}"}

    @staticmethod
    def _format_device_summary(info: Any) -> str:
        """Ringkas rest_device_info() (sama seperti CLI device-info samsungtvws)."""
        if not isinstance(info, dict):
            return "TV connected"

        device = info.get("device")
        if not isinstance(device, dict):
            device = info

        name = device.get("name") or info.get("name") or "-"
        model = device.get("model") or device.get("modelName") or "-"
        power = device.get("PowerState") or "-"
        os_name = device.get("OS") or "-"
        firmware = device.get("firmwareVersion") or "-"
        wifi_mac = device.get("wifiMac") or "-"
        resolution = device.get("resolution") or "-"
        network = device.get("networkType") or "-"

        return (
            f"TV terhubung\n"
            f"Nama: {name}\n"
            f"Model: {model}\n"
            f"Power: {power}\n"
            f"OS: {os_name}\n"
            f"Firmware: {firmware}\n"
            f"Resolusi: {resolution}\n"
            f"Jaringan: {network}\n"
            f"Wi‑Fi MAC: {wifi_mac}"
        )
