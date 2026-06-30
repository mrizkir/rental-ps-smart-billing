import os
from unittest.mock import MagicMock, patch

import pytest

from config import BASE_DIR
from tv_controller import SamsungTVController, resolve_token_file


class TestResolveTokenFile:
    def test_relative_path_resolved_to_base_dir(self):
        result = resolve_token_file("tv_token.txt")
        assert result == os.path.join(BASE_DIR, "tv_token.txt")

    def test_absolute_path_unchanged(self):
        absolute = "/tmp/tv_token.txt"
        assert resolve_token_file(absolute) == absolute


class TestSamsungTVController:
    @patch("tv_controller.send_magic_packet")
    def test_power_on_success(self, mock_wol, tv_controller):
        result = tv_controller.power_on()

        mock_wol.assert_called_once_with("80:8a:bd:9b:3c:02")
        assert result["success"] is True
        assert "Wake-on-LAN" in result["message"]

    @patch("tv_controller.send_magic_packet", side_effect=OSError("network error"))
    def test_power_on_failure(self, mock_wol, tv_controller):
        result = tv_controller.power_on()

        assert result["success"] is False
        assert "Wake-on-LAN failed" in result["message"]

    @patch("tv_controller.SamsungTVWS")
    def test_power_off_success(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_off()

        mock_tv_class.assert_called_once()
        mock_tv.send_key.assert_called_once_with("KEY_POWER")
        assert result["success"] is True

    @patch("tv_controller.SamsungTVWS", side_effect=ConnectionError("refused"))
    def test_power_off_failure(self, mock_tv_class, tv_controller):
        result = tv_controller.power_off()

        assert result["success"] is False
        assert "Power off failed" in result["message"]

    @patch("tv_controller.SamsungTVWS")
    def test_set_hdmi_success(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv_class.return_value = mock_tv

        result = tv_controller.set_hdmi()

        mock_tv.send_key.assert_called_once_with("KEY_HDMI1")
        assert result["success"] is True
        assert "HDMI1" in result["message"]

    @patch("tv_controller.SamsungTVWS")
    def test_send_key_success(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv_class.return_value = mock_tv

        result = tv_controller.send_key("KEY_VOLUP")

        mock_tv.send_key.assert_called_once_with("KEY_VOLUP")
        assert result["success"] is True

    @patch("tv_controller.SamsungTVWS")
    def test_open_browser_success(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv_class.return_value = mock_tv

        result = tv_controller.open_browser("http://192.168.1.10:8080/splash.html")

        mock_tv.open_browser.assert_called_once_with("http://192.168.1.10:8080/splash.html")
        assert result["success"] is True

    @patch("tv_controller.SamsungTVWS")
    def test_get_status_success(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"name": "Samsung TV"}}
        mock_tv_class.return_value = mock_tv

        result = tv_controller.get_status()

        assert result["success"] is True
        assert result["data"]["device"]["name"] == "Samsung TV"

    @patch("tv_controller.SamsungTVWS", side_effect=TimeoutError("timeout"))
    def test_get_status_failure(self, mock_tv_class, tv_controller):
        result = tv_controller.get_status()

        assert result["success"] is False
        assert "TV not reachable" in result["message"]
