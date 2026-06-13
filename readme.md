# 🚗 VDSO — Vehicle, Drive, Shoot, Shoot Outside
> *"Vidiso"* — Action Shooter Vehicle Game

[![Status](https://img.shields.io/badge/status-in%20development-yellow)]()
[![Platform](https://img.shields.io/badge/platform-PC%20%7C%20Mobile-blue)]()
[![Mode](https://img.shields.io/badge/mode-Online%20Multiplayer-green)]()
[![Version](https://img.shields.io/badge/GDD-v0.1%20DRAFT-orange)]()

---

## 📋 Daftar Isi
- [Gambaran Umum](#-gambaran-umum)
- [Mekanisme Gameplay](#-mekanisme-gameplay)
- [Sistem Kendaraan](#-sistem-kendaraan)
- [Sistem Senjata](#-sistem-senjata)
- [Sistem Karakter](#-sistem-karakter)
- [Mode Permainan](#-mode-permainan)
- [Progression & Monetisasi](#-progression--monetisasi)
- [Catatan Desain & TBD](#-catatan-desain--tbd)

---

## 🎯 Gambaran Umum

VDSO (dibaca: **Vidiso**) adalah game **action shooter berbasis kendaraan** dengan perspektif third-person. Pemain mengendalikan kendaraan bersenjata dalam pertempuran online, di mana setiap kendaraan bisa dikonfigurasi dengan berbagai jenis senjata menggunakan **sistem modular berbasis blok**.

### Ringkasan Cepat

| Kategori | Detail |
|---|---|
| Genre | Action Shooter Vehicle |
| Perspektif | Third-Person Shooter (TPS) |
| Platform | PC & Mobile |
| Mode | Online Multiplayer |
| Art Style | Semi-Realistic |
| Model Bisnis | Free to Play |

### Fitur Utama
- 🔫 **3 tipe senjata** — Driver Controlled, Outside Controlled, Automatic
- 🧱 **Sistem loadout berbasis blok** — 1 blok = 25 cm, dipasang di hardpoint
- 🚙 **3 tipe kendaraan** — Custom, Mix, Fixed
- 💥 **Damage per modul** — sasis, senjata, roda, mesin punya HP & DEF sendiri
- 🧍 **3 state pemain** — driver, gunner, infantry
- 🎮 **Mode awal** — Team Deathmatch 5v5
- 🔬 **Progression** — Research + Gacha (balance ketat)

---

## 🎮 Mekanisme Gameplay

### Kamera

Default perspektif adalah **TPS**. Beberapa kendaraan/senjata punya kamera sekunder yang aktif saat scope atau masuk mode gunner.

| Mode Kamera | Kapan Aktif | Keterangan |
|---|---|---|
| Third-Person (TPS) | Default | Kamera utama saat mengemudi & infantry |
| Secondary Camera | Manual / saat scope | Kamera khusus senjata tertentu |
| Gunner View | Masuk mode gunner | FPS/scope saat operasikan senjata outside |

---

### State Pemain

Setiap pemain punya tiga kemungkinan state. Transisi dilakukan aktif oleh pemain.

| State | Deskripsi | Mobilitas | Dapat Menembak |
|---|---|---|---|
| **Driver** | Mengemudikan kendaraan | Penuh (ikut kendaraan) | Senjata driver-controlled saja |
| **Gunner** | Operasikan senjata outside-controlled | Tidak bisa bergerak | Ya (senjata outside) |
| **Infantry** | Di luar kendaraan, jalan kaki | Bebas bergerak sendiri | Ya (senjata bawaan karakter) |

> **Catatan:** Saat driver berpindah ke mode gunner, kendaraan berhenti. Butuh minimal 2 orang agar kendaraan bisa bergerak sekaligus nembak dari senjata outside-controlled.

---

### Infantry State

Pemain masuk infantry state karena:
- Kendaraan meledak/hancur
- Sengaja keluar dari kendaraan
- Mau jadi gunner di kendaraan lain

Saat infantry, pemain bisa:
- ✅ Bergerak bebas di peta
- ✅ Pakai senjata bawaan karakter
- ✅ Naik kendaraan sendiri atau milik rekan tim
- ✅ Naik ke posisi outside-controlled gun di kendaraan musuh (kalau kosong)
- ❌ Tidak bisa ambil alih kendaraan musuh yang masih dikendarai

---

## 🚙 Sistem Kendaraan

### Tipe Kendaraan

Semua kendaraan adalah **preset tetap** (basis tidak bisa diubah), tapi dibedakan dari fleksibilitas senjatanya.

| Tipe | Senjata Bawaan | Slot Custom | Fleksibilitas |
|---|---|---|---|
| **Tipe 1 — Custom** | Tidak ada | Semua hardpoint bebas | ⭐⭐⭐ Tertinggi |
| **Tipe 2 — Mix** | Ada (tidak bisa dicopot) | Sebagian hardpoint tersisa | ⭐⭐ Menengah |
| **Tipe 3 — Fixed** | Semua fixed | Tidak ada | ⭐ Tidak ada |

---

### Modul Kendaraan & Damage

Setiap kendaraan punya beberapa modul independen, masing-masing dengan HP & DEF sendiri.

| Modul | Fungsi | Efek jika HP = 0 |
|---|---|---|
| **Sasis** | HP utama kendaraan | 💀 Kendaraan hancur total |
| **Mesin** | Penggerak kendaraan | HP rendah → melambat; HP 0 → mati / **70% chance meledak** |
| **Roda / Ban** | Mobilitas & handling | Hancur per ban, efek tergantung posisi & konfigurasi drive |
| **Senjata** | Unit penyerang | HP berkurang → stat turun; HP 0 → senjata tidak bisa dipakai |

#### Detail Roda
Efek kerusakan tergantung posisi dan konfigurasi kendaraan (AWD / RWD / FWD / rear-steer):

- 🔴 **Ban depan rusak** → susah belok
- 🔴 **Ban belakang rusak** → kendaraan sliding / tidak stabil
- Efek bervariasi per konfigurasi drive

#### Detail Mesin
Kerusakan mesin bersifat **bertahap**:
1. HP mesin rendah → kecepatan maksimal berkurang proporsional
2. HP mesin = 0 → mesin mati, kendaraan tidak bisa bergerak
3. **70% peluang meledak** saat HP = 0 → splash damage ke modul sekitar

#### Supir & Kematian
- Supir punya HP sendiri, bisa mati di dalam kendaraan
- Jika supir mati → kendaraan berhenti, tidak bisa diambil alih
- Desain kendaraan harus mempertimbangkan perlindungan supir

---

## 🔫 Sistem Senjata

### Tipe Senjata

#### 1. Driver Controlled
Dikendalikan langsung oleh driver. Bisa statis (fixed forward) atau bisa diputar (mode TPS). Tidak aktif jika driver berpindah ke mode gunner/infantry.

#### 2. Outside Controlled
Dioperasikan dari posisi terpisah (contoh: senapan mesin di bak hilux).

| Kondisi | Efek |
|---|---|
| 1 orang (driver jadi gunner) | Kendaraan **tidak bisa bergerak** |
| 2 orang (driver + gunner) | Kendaraan **bisa bergerak** sekaligus menembak |

Gunner punya kamera/view sendiri untuk aiming.

#### 3. Automatic (Otomatic)
Sistem targeting otomatis (aimbot). Menembak tanpa input manual.
- Bisa diatur prioritas target
- Range & akurasi tergantung spesifikasi senjata
- Cocok untuk menangani ancaman sekunder saat driver fokus nyetir

---

### Sistem Hardpoint & Blok

> **1 blok = 25 cm × 25 cm**

- Senjata dipasang di **hardpoint** yang tersedia di kendaraan
- Setiap hardpoint punya ukuran tertentu (contoh: `1x2`, `2x4`, `3x6` blok)
- Senjata lebih kecil dari hardpoint → tetap bisa dipasang (sisa ruang kosong)
- Tidak ada batas maksimum blok total — tergantung jumlah & ukuran hardpoint

| Ukuran Senjata | Hardpoint Minimum | Contoh Senjata |
|---|---|---|
| `1×2` blok | 1×2 | Senapan mesin ringan |
| `2×3` blok | 2×3 | Kanon otomatis |
| `2×4` blok | 2×4 | Meriam sedang |
| `3×6` blok | 3×6 | Meriam berat / howitzer |

**Contoh:** Bus dengan 4 hardpoint `3×6` bisa menampung 4 senjata besar sekaligus.

---

### Amunisi
- Amunisi **terbatas** per match
- Tidak ada regenerasi otomatis
- Isi ulang di **zona resupply** yang tersebar di peta

---

## 🧍 Sistem Karakter

### Gambaran Umum
Karakter dipilih sebelum masuk match dan menentukan:
- Senjata infantry yang dibawa
- Satu ability (aktif **atau** pasif)
- Bonus spesialis

### Spesialisasi

| Peran | Contoh Bonus |
|---|---|
| **Driver** | Peningkatan handling, akselerasi, ketahanan sasis |
| **Gunner** | Peningkatan akurasi, damage, kecepatan rotasi senjata |
| **Infantry** | Peningkatan mobilitas, HP, efektivitas senjata infantry |
| **Support** | Aura buff ke rekan tim, ability yang mempengaruhi kendaraan tim |

### Senjata Infantry
Bervariasi antar karakter — dari senjata ringan (pistol, SMG) hingga senjata anti-kendaraan (RPG, AT rifle). Tidak menggunakan sistem blok, bagian dari kit karakter.

---

## 🏟️ Mode Permainan

### Team Deathmatch 5v5 (Mode Awal)

| Parameter | Detail |
|---|---|
| Format | 5 vs 5 |
| Tipe | Team Deathmatch |
| Fokus | Vehicle combat |
| Queue | Solo queue (awal) |
| Bot | Tersedia, mengendarai kendaraan |

### Respawn & Loadout
Saat mati → diarahkan ke layar **pemilihan loadout** sebelum kembali ke battle. Pemain bisa ganti kendaraan, senjata, dan karakter dari koleksi yang dimiliki.

### Zona Resupply
Tersebar di setiap peta. Kendaraan yang masuk zona ini bisa **isi ulang amunisi**. Posisi zona bisa jadi titik strategis yang diperebutkan.

### Peta
Beberapa peta dengan variasi lingkungan:
- 🏜️ **Area terbuka** (padang, gurun) — pertempuran jarak jauh
- 🏙️ **Urban / perkotaan** — banyak cover & sudut tembak
- Beberapa peta punya **destructible environment**

---

## 📈 Progression & Monetisasi

### Model Bisnis
**Free to Play** dengan monetisasi melalui sistem **Gacha** (balance ketat).

### Gacha
Pool gacha mencakup:
- Karakter baru
- Kendaraan baru
- Senjata baru

> Semua item gacha diimbangi dengan item yang bisa didapat melalui gameplay (research/grind) — **anti pay-to-win** adalah prioritas utama.

### Research
Digunakan untuk:
- Upgrade statistik item yang sudah dimiliki
- Unlock item alternatif tanpa gacha

Resource research didapat dari: hasil match, daily/weekly quest, dan event.

| Jalur | Cara Dapat Item | Cara Upgrade |
|---|---|---|
| **Gacha** | Karakter, kendaraan, senjata baru | Research |
| **Research / Grind** | Item alternatif | Research |

---

## 📝 Catatan Desain & TBD

### ✅ Sudah Dikonfirmasi
- [x] Sistem blok hardpoint (1 blok = 25 cm)
- [x] 3 tipe senjata: Driver Controlled, Outside Controlled, Automatic
- [x] 3 tipe kendaraan: Custom, Mix, Fixed
- [x] Damage per modul dengan HP & DEF masing-masing
- [x] State pemain: driver, gunner, infantry
- [x] Respawn dengan pemilihan loadout
- [x] Amunisi terbatas + zona resupply
- [x] Mode awal: Team Deathmatch 5v5 dengan bot
- [x] Art style: semi-realistic
- [x] Platform: PC & Mobile
- [x] Model: Free to Play + Gacha + Research

### 🔲 Masih Perlu Dikembangkan (TBD)
- [ ] Daftar lengkap kendaraan (nama, tipe, spesifikasi)
- [ ] Daftar lengkap senjata (nama, ukuran blok, damage, range)
- [ ] Roster karakter awal
- [ ] Detail mekanisme online / multiplayer
- [ ] Detail UI/UX (lobby, loadout screen, HUD)
- [ ] Detail peta (layout, destructible, zona resupply)
- [ ] Sistem ekonomi gacha (rates, pity system)
- [ ] Detail balance gacha vs research
- [ ] Sistem quest / daily mission
- [ ] Audio direction
- [ ] Lore / worldbuilding (opsional)

### ⚠️ Risiko Desain
- **Kompleksitas loadout** → perlu tutorial yang kuat untuk pemain baru
- **Balance Fixed vs Custom** → kendaraan Fixed yang powerful vs Custom yang fleksibel harus seimbang
- **Gacha di F2P** → pity system dan transparansi rate sangat penting untuk kepercayaan komunitas
- **Transisi state di mobile** → perpindahan driver → gunner → infantry harus responsif & tidak frustasi

---

<div align="center">

*GDD v0.1 — DRAFT*
*Living document, akan terus diperbarui*

</div>