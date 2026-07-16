import pytest

from tv_controller import SamsungTVController


@pytest.fixture
def tv_controller(tmp_path, monkeypatch):
    monkeypatch.setattr("tv_controller.TOKENS_DIR", str(tmp_path))
    return SamsungTVController(
        tv_ip="192.168.100.92",
        tv_mac="80:8a:bd:9b:3c:02",
        token="test-token",
    )


@pytest.fixture
def flask_client():
    from tv_notification_store import clear_all
    from tv_session_overlay_store import clear_all_sessions
    from tv_service import app

    clear_all()
    clear_all_sessions()
    app.config["TESTING"] = True
    with app.test_client() as client:
        yield client
    clear_all()
    clear_all_sessions()
