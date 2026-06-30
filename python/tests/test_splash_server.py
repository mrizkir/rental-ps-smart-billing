import socket
import urllib.request
from unittest.mock import patch

import pytest

from splash_server import SplashServer, get_local_ip


def _free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


class TestGetLocalIp:
    def test_returns_ip_on_success(self):
        with patch("splash_server.socket.socket") as mock_socket_class:
            mock_sock = mock_socket_class.return_value.__enter__.return_value
            mock_sock.getsockname.return_value = ("192.168.1.50", 0)

            assert get_local_ip() == "192.168.1.50"

    def test_returns_localhost_on_failure(self):
        with patch("splash_server.socket.socket", side_effect=OSError("no network")):
            assert get_local_ip() == "127.0.0.1"


class TestSplashServer:
    @pytest.fixture
    def server(self):
        port = _free_port()
        srv = SplashServer(host="127.0.0.1", port=port)
        yield srv
        srv.stop()

    def test_start_and_stop(self, server):
        assert server.is_running is False

        server.start()
        assert server.is_running is True

        server.start()
        assert server.is_running is True

        server.stop()
        assert server.is_running is False

    def test_get_url_contains_params(self, server):
        with patch("splash_server.get_local_ip", return_value="192.168.1.50"):
            url = server.get_url(unit=1, durasi="01:00:00", nama="Budi")

        assert url.startswith("http://192.168.1.50:")
        assert "unit=1" in url
        assert "durasi=01%3A00%3A00" in url
        assert "nama=Budi" in url
        assert "rental=Rental" in url
        assert "Smart" in url
        assert "Billing" in url

    def test_serves_splash_html(self, server):
        server.start()

        with patch("splash_server.get_local_ip", return_value="127.0.0.1"):
            url = server.get_url(unit=2, durasi="02:00:00", nama="Andi")

        with urllib.request.urlopen(url, timeout=5) as response:
            html = response.read().decode("utf-8")

        assert response.status == 200
        assert "Rental PS Smart Billing" in html
        assert "setTimeout" in html
        assert 'id="unit"' in html
        assert 'id="nama"' in html
        assert 'id="durasi"' in html
