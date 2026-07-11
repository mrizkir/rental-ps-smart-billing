import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

DEFAULT_TV_IP = "192.168.100.92"
DEFAULT_TV_MAC = "80:8a:bd:9b:3c:02"
DEFAULT_PS4_MAC = "AA:BB:CC:DD:EE:FF"

FLASK_HOST = "127.0.0.1"
FLASK_PORT = 5001

SPLASH_HOST = "0.0.0.0"
SPLASH_PORT = 8080
# IP LAN PC kasir yang bisa diakses Smart TV (jangan pakai 127.0.0.1)
SPLASH_PUBLIC_IP = "192.168.100.5"
SPLASH_RENTAL_NAME = "Rental PS Smart Billing"

TV_WS_PORT = 8002
