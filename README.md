# rental-ps-smart-billing

Aplikasi kasir desktop untuk usaha rental PlayStation dengan kontrol Smart TV otomatis via jaringan lokal. Dirancang untuk operator non-teknis dengan antarmuka sederhana.

Aplikasi desktop (.NET) mengelola data kasir dan master Smart TV. Kontrol TV fisik (power on/off, cek status) dijalankan oleh layanan Python terpisah di mesin yang sama.

## Prasyarat

| Komponen | Versi |
|----------|-------|
| .NET SDK | 9.0+ |
| Python | 3.10+ |
| SQL Server | 2019+ (atau SQL Server di Docker) |

Unduh **.NET SDK 9**: [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0) — pilih **SDK** (bukan Runtime), lalu installer sesuai OS (Windows x64 / macOS / Linux).

Pastikan PC kasir dan TV Samsung berada di **jaringan LAN yang sama**.

---

## Cara mendapatkan data Smart TV

Data ini diisi di form **Smart TV → Tambah / Edit** di aplikasi.

| Field | Contoh | Wajib |
|-------|--------|-------|
| Nama TV | `TV Ruang A` | Ya |
| Merek | `Samsung` | Ya |
| IP Address | `192.168.1.100` | Ya |
| MAC Address | `80:8A:BD:9B:3C:02` | Ya |
| Port WebSocket | `8002` | Ya (default biasanya cukup) |
| Token Pairing | `SSSDDFFG123` | Opsional di awal; terisi setelah pairing |

### 1. IP Address

**Dari menu TV Samsung:**

1. Nyalakan TV
2. Buka **Settings** (ikon gear) → **General** / **Network** → **Network Status** / **About**
3. Pilih jaringan Wi-Fi atau Ethernet yang aktif
4. Catat **IP address** (contoh: `192.168.1.100`)

**Tips:** Di router/modem, sebaiknya set **DHCP reservation** (IP tetap) berdasarkan MAC TV agar IP tidak berubah setelah TV restart.

**Cek dari PC (opsional):**

```bash
# macOS / Linux — ganti nama device jika perlu
ping -c 2 192.168.1.100

# Lihat perangkat di LAN (contoh)
arp -a | grep -i samsung
```

### 2. MAC Address

**Dari menu TV Samsung:**

1. **Settings** → **General** / **Network** → **Network Status** / **About**
2. Catat **MAC Address** (Wi-Fi atau Wired/Ethernet — sesuaikan dengan koneksi yang dipakai)
3. Format di aplikasi: `XX:XX:XX:XX:XX:XX` (huruf besar, dipisah titik dua)

Contoh: `80:8a:bd:9b:3c:02` → tulis `80:8A:BD:9B:3C:02`

**Catatan Wake-on-LAN:** Fitur nyalakan TV dari aplikasi memakai MAC. Pastikan:

- TV terhubung ke jaringan (Wi-Fi/Ethernet)
- Di beberapa model: **Settings → General → Network → Expert Settings** → aktifkan opsi terkait remote / power on via jaringan (nama menu berbeda per model)

### 3. Port WebSocket

Untuk TV Samsung Tizen modern (sekitar 2016 ke atas) yang dikontrol lewat API:

| Port | Keterangan |
|------|------------|
| `8002` | **Default** — WebSocket + SSL (paling umum) |
| `8001` | WebSocket tanpa SSL (model lama / kasus khusus) |

Isi **`8002`** kecuali TV Anda memang memakai port lain. Field ini sudah ber-default `8002` di form.

### 4. Token Pairing

Token **bukan** dari menu Settings TV. Token muncul setelah PC “diizinkan” sebagai remote di layar TV.

**Langkah pairing:**

1. Pastikan Python TV service sudah jalan (`python tv_service.py`)
2. Login aplikasi sebagai `admin` → **Smart TV → Tambah Smart TV**
3. Isi Nama, IP, MAC, Port (`8002`); **Token biarkan kosong**
4. Klik **Test Koneksi**
5. Di layar TV, muncul permintaan izin / PIN — pilih **Allow** / izinkan
6. Jika berhasil, field **Token** terisi otomatis (string seperti `SSSDDFFG123`)
7. Klik **Simpan** — token tersimpan di database

Ulangi **Test Koneksi** kapan saja untuk memverifikasi TV masih terkoneksi. Jika token berubah setelah pairing ulang, aplikasi akan memperbarui nilai di DB saat test dari daftar TV.

### Checklist sebelum Test Koneksi

- [ ] TV dan PC kasir di Wi-Fi/LAN yang sama
- [ ] IP dan MAC sudah benar (MAC sesuai Wi-Fi **atau** Ethernet yang aktif)
- [ ] TV dalam keadaan menyala
- [ ] `python tv_service.py` sudah running
- [ ] Port `8002` (kecuali Anda tahu TV memakai port lain)

---

## 1. Database (SQL Server)

Aplikasi memakai database `rental_ps`. Skema dan data awal dibuat **otomatis** saat aplikasi pertama kali dijalankan.

Connection string default ada di [`app/appsettings.json`](app/appsettings.json). Untuk password lokal, buat file override (tidak di-commit ke git):

**`app/appsettings.local.json`**

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=rental_ps;User Id=sa;Password=PASSWORD_ANDA;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;"
  }
}
```

Atau lewat environment variable:

```bash
export RENTAL_PS_ConnectionStrings__Default="Server=localhost,1433;Database=rental_ps;User Id=sa;Password=PASSWORD_ANDA;Encrypt=True;TrustServerCertificate=True;Connect Timeout=30;"
```

Uji koneksi database tanpa membuka UI:

```bash
cd app
dotnet run -- --test-db
```

---

## 2. Aplikasi Desktop (.NET)

### Instalasi & build

```bash
cd app
dotnet restore
dotnet build
```

### Menjalankan

```bash
cd app
dotnet run
```

Mode verbose (log startup lebih detail):

```bash
cd app
dotnet run -- --verbose
```

Saat pertama kali jalan, aplikasi akan:
1. Membuat database `rental_ps` jika belum ada
2. Membuat tabel (Users, Roles, SmartTvs, dll.)
3. Mengisi akun default (jika database masih kosong)

### Akun default

| Username | Password | Role |
|----------|----------|------|
| `admin` | `Admin123!` | superadmin |
| `operator1` | `Operator123!` | operator |

**superadmin** dapat mengelola user dan Smart TV. **operator** hanya fitur kasir (billing — masih dalam pengembangan).

### Konfigurasi TV service

Di [`app/appsettings.json`](app/appsettings.json):

```json
"TvService": {
  "BaseUrl": "http://127.0.0.1:5001"
}
```

`BaseUrl` harus mengarah ke Python TV service (lihat bagian berikutnya). Token pairing per TV disimpan di kolom `SmartTvs.Token` di database (bukan file).

---

## 3. Layanan Python (Kontrol TV)

Layanan Flask ini mengontrol TV Samsung via WebSocket dan Wake-on-LAN. Wajib dijalankan **sebelum** fitur test koneksi / kontrol TV dipakai dari aplikasi desktop.

### Instalasi dependensi

Disarankan memakai virtual environment:

```bash
cd python
python -m venv venv
source venv/bin/activate        # macOS / Linux
# venv\Scripts\activate         # Windows

pip install -r requirements.txt
```

### Konfigurasi (opsional)

Default ada di [`python/config.py`](python/config.py):

| Setting | Default | Keterangan |
|---------|---------|------------|
| `FLASK_HOST` | `127.0.0.1` | Host Flask |
| `FLASK_PORT` | `5001` | Port Flask |
| `TV_WS_PORT` | `8002` | Port WebSocket Samsung (fallback) |
| `SPLASH_PUBLIC_IP` | `192.168.100.5` | IP LAN PC kasir (URL yang dibuka TV) |
| `SPLASH_PORT` | `8080` | Port server splash |

Per-TV (IP, MAC, port, **token string**) dikirim dari aplikasi .NET saat test koneksi — Python **tidak** mengakses SQL Server.

### Menjalankan

```bash
cd python
source venv/bin/activate   # jika pakai venv
python tv_service.py
```

Output yang diharapkan:

```
Starting TV service on http://127.0.0.1:5001
```

Cek service hidup:

```bash
curl http://127.0.0.1:5001/health
```

### Pairing token Samsung TV

Token pairing adalah string (contoh: `SSSDDFFG123`), disimpan di database oleh aplikasi .NET (`SmartTvs.Token`).

1. Tambah/edit Smart TV di aplikasi (Token boleh kosong)
2. Jalankan **Test Koneksi** — TV meminta approve pairing di layar
3. Python mengembalikan token baru di response
4. .NET mengisi field Token / menyimpan ke DB

Python hanya menerima `tv_token` dari request dan (jika ada token baru) mengembalikannya — tanpa menulis file atau database.

### Menjalankan unit test Python

```bash
cd python
source venv/bin/activate
pip install -r requirements.txt
pytest tests/ -q
```

---

## 4. Menjalankan Keduanya (alur normal)

Buka **dua terminal**:

**Terminal 1 — Python TV service**

```bash
cd python
source venv/bin/activate
python tv_service.py
```

**Terminal 2 — Aplikasi desktop**

```bash
cd app
dotnet run -- --verbose
```

Lalu login sebagai `admin` / `Admin123!` → menu **Smart TV** untuk kelola dan test koneksi TV.

---

## 5. Troubleshooting

| Masalah | Solusi |
|---------|--------|
| `Login failed for user 'sa'` | Password di `appsettings.local.json` harus sama dengan yang dipakai di DBeaver. Uji: `dotnet run -- --test-db` |
| `Python TV service tidak berjalan` | Pastikan `python tv_service.py` sudah jalan di terminal terpisah |
| Test koneksi TV gagal | Cek IP/MAC TV, pastikan TV menyala & di LAN yang sama, lakukan pairing token jika belum |
| Menu Smart TV tidak muncul | Login sebagai superadmin (`admin`), bukan operator |

---

## Struktur proyek

```
app/          # Aplikasi desktop Avalonia (.NET 9)
python/       # Layanan Flask kontrol Smart TV Samsung
sql/          # Referensi skema SQL (opsional, DB diinisialisasi otomatis)
```
