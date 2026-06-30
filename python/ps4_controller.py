import logging

from wakeonlan import send_magic_packet

logger = logging.getLogger(__name__)


class PS4Controller:
    def power_on(self, mac_address: str) -> dict:
        try:
            logger.info("Sending Wake-on-LAN to PS4 %s", mac_address)
            send_magic_packet(mac_address)
            return {"success": True, "message": "Wake-on-LAN packet sent to PS4"}
        except Exception as exc:
            logger.exception("Failed to send Wake-on-LAN to PS4 %s", mac_address)
            return {"success": False, "message": f"PS4 Wake-on-LAN failed: {exc}"}
