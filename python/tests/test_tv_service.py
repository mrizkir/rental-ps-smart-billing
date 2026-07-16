from unittest.mock import MagicMock, patch


class TestHealthEndpoint:
    def test_health_returns_ok(self, flask_client):
        response = flask_client.get("/health")

        assert response.status_code == 200
        data = response.get_json()
        assert data["success"] is True
        assert "running" in data["message"]


class TestTVEndpoints:
    @patch("tv_service.get_tv_controller")
    def test_tv_power_on(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.power_on.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post("/tv/power-on", json={})

        assert response.status_code == 200
        mock_controller.power_on.assert_called_once()

    @patch("tv_service.get_tv_controller")
    def test_tv_power_off(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.power_off.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post("/tv/power-off", json={})

        assert response.status_code == 200
        mock_controller.power_off.assert_called_once()

    @patch("tv_service.get_tv_controller")
    def test_tv_set_hdmi(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.set_hdmi.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post("/tv/set-hdmi", json={})

        assert response.status_code == 200
        mock_controller.set_hdmi.assert_called_once()

    @patch("tv_service.get_tv_controller")
    def test_tv_send_key_success(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.send_key.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post("/tv/send-key", json={"key": "KEY_VOLUP"})

        assert response.status_code == 200
        mock_controller.send_key.assert_called_once_with("KEY_VOLUP")

    def test_tv_send_key_missing_key(self, flask_client):
        response = flask_client.post("/tv/send-key", json={})

        assert response.status_code == 400
        assert response.get_json()["success"] is False

    @patch("tv_service.get_tv_controller")
    def test_tv_open_browser_success(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.open_browser.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post(
            "/tv/open-browser",
            json={"url": "http://example.com"},
        )

        assert response.status_code == 200
        mock_controller.open_browser.assert_called_once_with("http://example.com")

    def test_tv_open_browser_missing_url(self, flask_client):
        response = flask_client.post("/tv/open-browser", json={})

        assert response.status_code == 400
        assert response.get_json()["success"] is False

    @patch("tv_service.get_tv_controller")
    def test_tv_status_connected(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.get_status.return_value = {
            "success": True,
            "message": "TV connected",
            "data": {},
        }
        mock_get_controller.return_value = mock_controller

        response = flask_client.get("/tv/status")

        assert response.status_code == 200
        assert response.get_json()["success"] is True

    @patch("tv_service.get_tv_controller")
    def test_tv_status_unreachable(self, mock_get_controller, flask_client):
        mock_controller = MagicMock()
        mock_controller.get_status.return_value = {
            "success": False,
            "message": "TV not reachable",
        }
        mock_get_controller.return_value = mock_controller

        response = flask_client.get("/tv/status")

        assert response.status_code == 503
        assert response.get_json()["success"] is False


class TestPS4Endpoint:
    @patch("tv_service.ps4_controller")
    def test_ps4_power_on(self, mock_ps4_controller, flask_client):
        mock_ps4_controller.power_on.return_value = {
            "success": True,
            "message": "Wake-on-LAN packet sent to PS4",
        }

        response = flask_client.post(
            "/ps4/power-on",
            json={"ps4_mac": "AA:BB:CC:DD:EE:FF"},
        )

        assert response.status_code == 200
        mock_ps4_controller.power_on.assert_called_once_with("AA:BB:CC:DD:EE:FF")


class TestSplashEndpoint:
    @patch("tv_service.get_tv_controller")
    @patch("tv_service.splash_server")
    def test_splash_show_success(self, mock_splash_server, mock_get_controller, flask_client):
        mock_splash_server.get_url.return_value = "http://192.168.1.50:8080/splash.html?unit=1"
        mock_controller = MagicMock()
        mock_controller.open_browser.return_value = {"success": True, "message": "ok"}
        mock_get_controller.return_value = mock_controller

        response = flask_client.post(
            "/splash/show",
            json={"unit": 1, "durasi": "01:00:00", "nama": "Budi"},
        )

        assert response.status_code == 200
        data = response.get_json()
        assert data["success"] is True
        assert "url" in data
        mock_splash_server.start.assert_called_once()

    def test_splash_show_missing_fields(self, flask_client):
        response = flask_client.post("/splash/show", json={"unit": 1})

        assert response.status_code == 400
        assert response.get_json()["success"] is False

    @patch("tv_service.get_tv_controller")
    @patch("tv_service.splash_server")
    def test_splash_show_open_browser_failure(
        self, mock_splash_server, mock_get_controller, flask_client
    ):
        mock_splash_server.get_url.return_value = "http://192.168.1.50:8080/splash.html"
        mock_controller = MagicMock()
        mock_controller.open_browser.return_value = {
            "success": False,
            "message": "Open browser failed",
        }
        mock_get_controller.return_value = mock_controller

        response = flask_client.post(
            "/splash/show",
            json={"unit": 1, "durasi": "01:00:00", "nama": "Budi"},
        )

        assert response.status_code == 500
        assert response.get_json()["success"] is False


class TestDeviceConfig:
    @patch("tv_service.SamsungTVController")
    def test_tv_ip_and_mac_from_body(self, mock_controller_class, flask_client):
        mock_instance = MagicMock()
        mock_instance.power_on.return_value = {"success": True, "message": "ok"}
        mock_controller_class.return_value = mock_instance

        flask_client.post(
            "/tv/power-on",
            json={"tv_ip": "10.0.0.5", "tv_mac": "11:22:33:44:55:66"},
        )

        mock_controller_class.assert_called_once()
        kwargs = mock_controller_class.call_args.kwargs
        assert kwargs["tv_ip"] == "10.0.0.5"
        assert kwargs["tv_mac"] == "11:22:33:44:55:66"

    def test_tv_power_on_missing_ip_mac(self, flask_client):
        response = flask_client.post("/tv/power-on", json={})

        assert response.status_code == 400
        data = response.get_json()
        assert data["success"] is False
        assert "tv_ip" in data["message"]
        assert "tv_mac" in data["message"]

    def test_ps4_power_on_missing_mac(self, flask_client):
        response = flask_client.post("/ps4/power-on", json={})

        assert response.status_code == 400
        assert "ps4_mac" in response.get_json()["message"]


class TestTvNotificationEndpoint:
    def test_get_empty_returns_no_warning(self, flask_client):
        response = flask_client.get("/api/tv-notification?tv_id=1")

        assert response.status_code == 200
        data = response.get_json()
        assert data["show_warning"] is False
        assert data["message"] == ""

    def test_post_then_get_consumes_warning(self, flask_client):
        post = flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 1, "show_warning": True, "message": "5 menit lagi"},
        )
        assert post.status_code == 200
        assert post.get_json()["success"] is True

        first = flask_client.get("/api/tv-notification?tv_id=1")
        assert first.status_code == 200
        data = first.get_json()
        assert data["show_warning"] is True
        assert data["message"] == "5 menit lagi"

        second = flask_client.get("/api/tv-notification?tv_id=1")
        assert second.get_json()["show_warning"] is False

    def test_notifications_are_per_tv_id(self, flask_client):
        flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 1, "show_warning": True, "message": "TV1"},
        )
        flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 2, "show_warning": True, "message": "TV2"},
        )

        assert flask_client.get("/api/tv-notification?tv_id=1").get_json()["message"] == "TV1"
        assert flask_client.get("/api/tv-notification?tv_id=2").get_json()["message"] == "TV2"

    def test_post_clear_warning(self, flask_client):
        flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 1, "show_warning": True, "message": "5 menit lagi"},
        )
        flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 1, "show_warning": False},
        )

        data = flask_client.get("/api/tv-notification?tv_id=1").get_json()
        assert data["show_warning"] is False

    def test_post_default_message_when_empty(self, flask_client):
        flask_client.post(
            "/api/tv-notification",
            json={"tv_id": 1, "show_warning": True, "message": ""},
        )

        data = flask_client.get("/api/tv-notification?tv_id=1").get_json()
        assert data["show_warning"] is True
        assert data["message"] == "Waktu hampir habis"
