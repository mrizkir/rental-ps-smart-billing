from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from tv_controller import SamsungTVController


@pytest.fixture
def token_dir(tmp_path, monkeypatch):
    monkeypatch.setattr("tv_controller.TOKENS_DIR", str(tmp_path))
    return tmp_path


@pytest.fixture
def tv_controller(token_dir):
    return SamsungTVController(
        tv_ip="192.168.100.92",
        tv_mac="80:8a:bd:9b:3c:02",
        token="test-token",
    )


@pytest.fixture(autouse=True)
def fast_wol_wait(monkeypatch):
    monkeypatch.setattr("tv_controller.WOL_WAIT_SECONDS", 4)
    monkeypatch.setattr("tv_controller.WOL_POLL_INTERVAL", 0)
    monkeypatch.setattr("tv_controller.WOL_PACKET_COUNT", 1)


class TestSamsungTVController:
    @patch("tv_controller.time.sleep", return_value=None)
    @patch("tv_controller.send_magic_packet")
    @patch("tv_controller.SamsungTVWS")
    def test_power_on_from_standby_uses_key_power(
        self, mock_tv_class, mock_wol, _sleep, tv_controller
    ):
        instances = []

        def connect_side_effect(*args, **kwargs):
            tv = MagicMock()
            instances.append(tv)
            if len(instances) == 1:
                tv.rest_device_info.return_value = {"device": {"PowerState": "standby"}}
            else:
                tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
                tv.token = "tok-on"
            return tv

        mock_tv_class.side_effect = connect_side_effect

        result = tv_controller.power_on()

        assert result["success"] is True
        mock_wol.assert_not_called()
        instances[0].send_key.assert_called_once_with("KEY_POWER")
        assert "powered on" in result["message"].lower()

    @patch("tv_controller.time.sleep", return_value=None)
    @patch("tv_controller.send_magic_packet")
    @patch("tv_controller.SamsungTVWS")
    def test_power_on_waits_until_ready(self, mock_tv_class, mock_wol, _sleep, tv_controller):
        def connect_side_effect(*args, **kwargs):
            tv = MagicMock()
            if mock_wol.call_count == 0:
                tv.rest_device_info.side_effect = ConnectionError("off")
            elif mock_wol.call_count >= 1 and not hasattr(connect_side_effect, "after"):
                connect_side_effect.after = True
                tv.rest_device_info.side_effect = ConnectionError("still waking")
            else:
                tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
                tv.token = "tok1"
            return tv

        mock_tv_class.side_effect = connect_side_effect

        result = tv_controller.power_on()

        assert result["success"] is True
        mock_wol.assert_called_with("80:8a:bd:9b:3c:02")
        assert "powered on" in result["message"].lower() or "already on" in result["message"].lower()

    @patch("tv_controller.time.sleep", return_value=None)
    @patch("tv_controller.send_magic_packet")
    @patch("tv_controller.SamsungTVWS")
    def test_power_on_already_on_skips_wol(self, mock_tv_class, mock_wol, _sleep, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_on()

        assert result["success"] is True
        assert "already on" in result["message"].lower()
        mock_wol.assert_not_called()

    @patch("tv_controller.time.sleep", return_value=None)
    @patch("tv_controller.send_magic_packet", side_effect=OSError("network error"))
    @patch("tv_controller.SamsungTVWS")
    def test_power_on_wol_failure(self, mock_tv_class, mock_wol, _sleep, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.side_effect = ConnectionError("off")
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_on()

        assert result["success"] is False
        assert "failed" in result["message"].lower()

    @patch("tv_controller.SamsungTVWS")
    def test_power_off_when_on_sends_key(self, mock_tv_class, tv_controller, token_dir):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
        mock_tv.token = "SSSDDFFG123"
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_off()

        mock_tv.send_key.assert_called_once_with("KEY_POWER")
        assert result["success"] is True
        assert result["token"] == "SSSDDFFG123"
        assert (token_dir / "192.168.100.92.token").read_text(encoding="utf-8") == "SSSDDFFG123"

    @patch("tv_controller.SamsungTVWS")
    def test_power_off_skips_when_standby(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"PowerState": "standby"}}
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_off()

        mock_tv.send_key.assert_not_called()
        assert result["success"] is True
        assert "skipped" in result["message"].lower()

    @patch("tv_controller.SamsungTVWS")
    def test_power_off_skips_when_unreachable(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.side_effect = ConnectionError("refused")
        mock_tv_class.return_value = mock_tv

        result = tv_controller.power_off()

        mock_tv.send_key.assert_not_called()
        assert result["success"] is True
        assert "assumed already off" in result["message"].lower()

    @patch("tv_controller.SamsungTVWS")
    def test_power_off_failure_on_send_key(self, mock_tv_class, tv_controller):
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
        mock_tv.send_key.side_effect = ConnectionError("ws failed")
        mock_tv_class.return_value = mock_tv

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
        mock_tv.token = "NEWxxxx"
        mock_tv_class.return_value = mock_tv

        result = tv_controller.get_status()

        assert result["success"] is True
        assert result["data"]["device"]["name"] == "Samsung TV"
        assert result["token"] == "NEWxxxx"
        mock_tv.open.assert_called_once()
        mock_tv.close.assert_called_once()

    @patch("tv_controller.SamsungTVWS", side_effect=TimeoutError("timeout"))
    def test_get_status_failure(self, mock_tv_class, tv_controller):
        result = tv_controller.get_status()

        assert result["success"] is False
        assert "TV not reachable" in result["message"]

    def test_empty_token_normalized_to_none(self, token_dir):
        controller = SamsungTVController(
            tv_ip="192.168.1.1",
            tv_mac="AA:BB:CC:DD:EE:FF",
            token="   ",
        )
        assert controller.token is None

    @patch("tv_controller.SamsungTVWS")
    def test_reuses_cached_token_when_request_has_none(self, mock_tv_class, token_dir):
        token_path = token_dir / "192.168.1.1.token"
        token_path.write_text("cached-token\n", encoding="utf-8")

        controller = SamsungTVController(
            tv_ip="192.168.1.1",
            tv_mac="AA:BB:CC:DD:EE:FF",
            token=None,
        )
        mock_tv = MagicMock()
        mock_tv.rest_device_info.return_value = {"device": {"PowerState": "on"}}
        mock_tv_class.return_value = mock_tv

        controller.power_off()

        kwargs = mock_tv_class.call_args.kwargs
        assert kwargs.get("token") == "cached-token"
