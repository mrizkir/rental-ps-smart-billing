import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# 0.0.0.0 supaya Tizen app di TV (LAN) bisa reach /api/tv-notification.
# App desktop .NET tetap bisa pakai http://127.0.0.1:5001
FLASK_HOST = "0.0.0.0"
FLASK_PORT = 5001

SPLASH_HOST = "0.0.0.0"
SPLASH_PORT = 8080
# IP LAN PC kasir yang bisa diakses Smart TV (jangan pakai 127.0.0.1)
SPLASH_PUBLIC_IP = "192.168.100.5"
SPLASH_RENTAL_NAME = "Rental PS Smart Billing"

TV_WS_PORT = 8002
# Cache token pairing per-TV (samsungtvws membaca/menulis file ini)
TOKENS_DIR = os.path.join(BASE_DIR, "tokens")

# Power-on: tunggu TV siap setelah Wake-on-LAN sebelum splash
WOL_WAIT_SECONDS = 30
WOL_POLL_INTERVAL = 2
WOL_PACKET_COUNT = 3
