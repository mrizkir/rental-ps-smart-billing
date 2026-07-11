import http.server
import logging
import socket
import socketserver
import threading
import urllib.parse

from config import BASE_DIR, SPLASH_HOST, SPLASH_PORT, SPLASH_PUBLIC_IP, SPLASH_RENTAL_NAME

logger = logging.getLogger(__name__)


def get_local_ip() -> str:
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.connect(("8.8.8.8", 80))
            return sock.getsockname()[0]
    except OSError:
        return "127.0.0.1"


class SplashHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=BASE_DIR, **kwargs)

    def log_message(self, format, *args):
        logger.info("Splash server: %s - %s", self.address_string(), format % args)


class SplashServer:
    def __init__(self, host: str = SPLASH_HOST, port: int = SPLASH_PORT):
        self.host = host
        self.port = port
        self._httpd: socketserver.TCPServer | None = None
        self._thread: threading.Thread | None = None

    @property
    def is_running(self) -> bool:
        return self._httpd is not None

    def start(self) -> None:
        if self.is_running:
            logger.info("Splash server already running on port %s", self.port)
            return

        self._httpd = socketserver.TCPServer((self.host, self.port), SplashHTTPRequestHandler)
        self._httpd.allow_reuse_address = True
        self._thread = threading.Thread(target=self._httpd.serve_forever, daemon=True)
        self._thread.start()
        logger.info("Splash server started on %s:%s", self.host, self.port)

    def stop(self) -> None:
        if not self.is_running:
            return

        self._httpd.shutdown()
        self._httpd.server_close()
        self._httpd = None
        self._thread = None
        logger.info("Splash server stopped")

    def get_url(self, unit: int, durasi: str, nama: str, rental_name: str = SPLASH_RENTAL_NAME) -> str:
        params = urllib.parse.urlencode(
            {
                "unit": unit,
                "durasi": durasi,
                "nama": nama,
                "rental": rental_name,
            }
        )
        # IP manual di config — TV harus reach PC di alamat ini
        public_ip = (SPLASH_PUBLIC_IP or "").strip() or get_local_ip()
        return f"http://{public_ip}:{self.port}/splash.html?{params}"
