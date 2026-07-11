import pytest

from tv_controller import SamsungTVController


@pytest.fixture
def tv_controller():
    return SamsungTVController(
        tv_ip="192.168.100.92",
        tv_mac="80:8a:bd:9b:3c:02",
        token="test-token",
    )


@pytest.fixture
def flask_client():
    from tv_service import app

    app.config["TESTING"] = True
    with app.test_client() as client:
        yield client
