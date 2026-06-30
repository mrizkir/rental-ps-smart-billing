from unittest.mock import patch

from ps4_controller import PS4Controller


class TestPS4Controller:
    @patch("ps4_controller.send_magic_packet")
    def test_power_on_success(self, mock_wol):
        controller = PS4Controller()

        result = controller.power_on("AA:BB:CC:DD:EE:FF")

        mock_wol.assert_called_once_with("AA:BB:CC:DD:EE:FF")
        assert result["success"] is True
        assert "PS4" in result["message"]

    @patch("ps4_controller.send_magic_packet", side_effect=OSError("send failed"))
    def test_power_on_failure(self, mock_wol):
        controller = PS4Controller()

        result = controller.power_on("AA:BB:CC:DD:EE:FF")

        assert result["success"] is False
        assert "PS4 Wake-on-LAN failed" in result["message"]
