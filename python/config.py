import os

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# 0.0.0.0 supaya Tizen app di TV (LAN) bisa reach /api/tv-notification.
# App desktop .NET tetap bisa pakai http://127.0.0.1:5001
FLASK_HOST = "0.0.0.0"
FLASK_PORT = 5001

SPLASH_HOST = "0.0.0.0"
SPLASH_PORT = 8080
# IP LAN PC kasir yang bisa diakses Smart TV (jangan pakai 127.0.0.1)
SPLASH_PUBLIC_IP = "192.168.101.29"
SPLASH_RENTAL_NAME = "Rental PS Smart Billing"

TV_WS_PORT = 8002
# Cache token pairing per-TV (samsungtvws membaca/menulis file ini)
TOKENS_DIR = os.path.join(BASE_DIR, "tokens")

# Power-on: tunggu TV siap setelah Wake-on-LAN sebelum splash
WOL_WAIT_SECONDS = 30
WOL_POLL_INTERVAL = 2
WOL_PACKET_COUNT = 3

# Sleep Timer: KEY_SLEEP hanya membuka form; lanjut navigasi untuk set durasi.
# Mode:
#   menu  → KEY_SLEEP lalu SLEEP_TIMER_CONFIRM_KEYS (default: Down + Enter = 30 menit)
#   cycle → tekan KEY_SLEEP berulang (Off→30→60→…); press count dihitung otomatis
SLEEP_TIMER_MODE = os.environ.get("SLEEP_TIMER_MODE", "menu").strip().lower()
SLEEP_TIMER_MINUTES = int(os.environ.get("SLEEP_TIMER_MINUTES", "30"))
SLEEP_TIMER_KEY_DELAY = float(os.environ.get("SLEEP_TIMER_KEY_DELAY", "1.0"))
# Dipakai mode menu; sesuaikan per model TV jika urutan berbeda
SLEEP_TIMER_CONFIRM_KEYS = [
    key.strip()
    for key in os.environ.get("SLEEP_TIMER_CONFIRM_KEYS", "KEY_DOWN,KEY_ENTER").split(",")
    if key.strip()
]
# Urutan opsi di mode cycle (0/Off diabaikan; tekan pertama = opsi pertama)
SLEEP_TIMER_CYCLE_OPTIONS = [30, 60, 90, 120, 150, 180]
