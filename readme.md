# Projek Mobil Duuaar

Game ekstraksi kendaraan modular berbasis Unity. Build kendaraan, pasang modul, lalu terjun ke zona ekstraksi seluas 4x4 km buat cari loot, bertahan dari pengejaran musuh, dan kabur.

---

# GDD — Game Design Document (v0.1)

> Draft, akan terus diperbarui seiring keputusan desain.

## 1. Ringkasan Game

- **Genre**: Extraction Vehicle Combat, offline 100% (single player)
- **Setting**: Dunia post-apocalyptic gurun gaya Mad Max — perpaduan gurun pasir + kota rusak
- **Map**: Battlefield 4x4 km (16 km²)
- **Platform**: PC — Unity 2022.3 (URP)
- **Filosofi inti**: *"Semua bisa hilang, kecuali ilmu yang kamu bawa pulang."* Setiap raid adalah perjudian — makin dalam kamu menantang badai, makin besar hasilnya, makin besar risikonya.

## 2. Struktur Map & Alur

Ada **2 map terpisah** (scene berbeda):

### 2.1 Safe Zone (Garasi)
- Fungsi: menu utama + setup kendaraan + **tes mobil/modul** (area test)
- Isi:
  - Garasi (pasang/unpasang modul ke kendaraan)
  - Area tes (lari lintasan, uji tembak)
  - Titik **badai pasir aman (safe sandstorm)** — gerbang masuk ke Battlefield
- Tata letak: menyesuaikan (desain menyusul, prioritas: alur singkat garasi → test → gate)

### 2.2 Battlefield (4x4 km, gurun + kota)
- **4 jalanan utama** (gate utara, barat, timur, selatan) — transisi dari safe zone **seamless** (tetap aspal + lingkungan gurun, tanpa loading terasa)
- **Masuk: gate acak** — pemain tidak tahu akan muncul di gerbang mana (mendorong eksplorasi & orientasi lewat landmark)
- **Keluar: hanya 1 gate** yang punya badai pasir aman
  - Pinggir map lainnya = **badai pasir bahaya** (merusak kendaraan, **mencopot modul**)
- **Badai pasir bergerak** di dalam map: hanya menutup pandangan (visual/cover), bukan damage — biar tidak terlihat aneh badai statis
- **Siklus siang-malam** aktif

### 2.3 Alur Seamless
```
Safe Zone → (masuk badai pasir aman) → muncul di salah satu dari 4 gate → raid di map
   ↑                                                                       ↓
   └── loading seamless ke safe zone ←── masuk dalam ke badai pasir aman di gate ekstraksi
```
- Ekstraksi: pemain harus **masuk agak dalam** ke safe sandstorm → secara lore AI berhenti mengejar → loading seamless balik ke safe zone
- Area looting: **kampung-kampung kecil tersebar** (bukan 1 kota raksasa)

## 3. Gameplay Loop

```
Garasi (setup + tes)
   ↓
Raid di Battlefield (cari scrap & blueprint)
   ↓
Ekstraksi (ke gate aman) atau Mati (semua hilang)
   ↓
Craft & upgrade di Garasi
   ↓
Raid berikutnya (Heat naik seiring progres)
```

### 3.1 Saat di Kendaraan (Combat)
- Menembak lewat senjata/turret yang terpasang (modul)
- **Modul amunisi**: selama modul amunisi belum hancur (kena tembak) atau di-uninstall, peluru "unlimited" **tapi terbatas per raid** (amunisi tidak di-reset antar raid tanpa modul)
- Kerusakan: roda bisa hancur (pincang), modul bisa copot (explosion), part kritis bisa meledak

### 3.2 Saat Turun (Looting)
- Pemain turun hanya membawa **linggis + senter** — **tanpa senjata**
- Looting dominan dilakukan di bangunan (mirip PUBG)
- Risiko: AI menembak pemain yang turun (kaliber besar → langsung tewas)
- Hasil looting: **scrap** (material craft) & **blueprint**

### 3.3 Ekstraksi
- Capai 1 gate aman → masuk dalam ke badai pasir aman
- AI berhenti mengejar (lore: tidak berani masuk badai)
- Loading seamless kembali ke Safe Zone

## 4. Progression & Risiko

### 4.1 Kematian / Kendaraan Hancur
- **Semua barang bawaan hilang** (tidak ada loot di posisi mati — secara lore musuh menjarah barangmu)
- Kendaraan hancur berapa kali pun → dapat **mobil default** lagi (tanpa senjata, polos)
- Harus "main aman" dulu: kumpulkan resource untuk bangun senjata baru

### 4.2 Persistensi
- Scrap & blueprint yang sudah di-bank (di garasi) = **aman permanen**
- Modul & senjata terpasang ikut hilang bila kendaraan hancur / modul dicopot
- Badai bahaya bisa **mencopot modul** (hilang juga)

### 4.3 Progres Kendaraan (via Blueprint)
- Kendaraan tidak dibeli dengan uang — **di-unlock lewat blueprint**
- Tier kendaraan (rencana):
  1. Mobil default (polos, tanpa senjata)
  2. Buggy
  3. Pickup
  4. Truk lapis baja
  5. Tank
- **Mencuri kendaraan musuh**: kendaraan AI yang masih bergerak bisa dicuri (dibajak) — rencana sistem semi-truck, implementasi menyusul
- **Senjata musuh tidak bisa dicuri** sebelum blueprint senjatanya didapat

### 4.4 Heat System (ala Need for Speed: Most Wanted)
Sistem tekanan yang naik seiring aksi pemain di dalam raid:

| Heat | Pemicu (naik) | Efek di Map |
|------|---------------|-------------|
| Lv 1 | Awal raid | Patroli normal (campuran mobil polos & bersenjata) |
| Lv 2 | Hancurkan mobil AI / looting blueprint | Patroli lebih banyak, lebih sering bersenjata |
| Lv 3 | Semakin banyak aksi & waktu di raid | Armada bersenjata dominan |
| Lv 4 | Berkelanjutan | Unit elite muncul, pengejaran agresif |
| Lv 5 | Ekstrem | Markas faksi aktif penuh, semua jalan dijaga |

- **Heat reset**: berhasil ekstraksi ATAU mati
- Desain tujuan: pemain harus memilih *"cukup sampai sini"* — makin lama ngejar loot, makin berisiko pulang
- (Opsional lanjutan) Heat tinggi = drop reward lebih bagus (insentif berani)

### 4.5 Tujuan Akhir (Endgame)
- **Markas faksi** (1-2 lokasi di pojok map): dijaga AI terkuat + boss
  - Bersihkan → **blueprint unik** (senjata/kendaraan tier tinggi) + scrap banyak
  - Dapat diulang dengan reward lebih kecil
- Endless tetap berjalan setelah semua tercapai (koleksi lengkap, min-max build)

## 5. AI & Musuh

- **Faksi musuh**: AI kendaraan patroli (jalanan utama, keliling map)
  - **Bersenjata** → menembak pemain
  - **Polos (tanpa senjata)** → aman, tidak menyerang (asal pemain tetap di kendaraan)
- **Pemain turun dari kendaraan** → langsung ditembak (kaliber besar, nyaris instan tewas)
- **Markas faksi**: patroli elite, posisi statis, boss; masuk sebagai tujuan utama
- Patroli & kekuatan armada dipengaruhi **Heat level**

## 6. Kendaraan, Modul, Loot & Craft

### 6.1 Loot
| Item | Fungsi |
|------|--------|
| Scrap | Material dasar crafting |
| Blueprint | Buka resep senjata/modul/kendaraan baru |

### 6.2 Crafting (di Garasi)
- Scrap + blueprint → senjata, modul, upgrade
- Tidak bisa mencuri senjata musuh sebelum blueprint-nya didapat

### 6.3 Modul (sistem grid yang sudah ada)
- Senjata (turret, meriam, dsb)
- Amunisi (memberi amunisi "unlimited per raid" selama modul utuh)
- Armor, roda, part kritis, modul volatile (meledak)

## 7. Keputusan yang Masih Terbuka (TODO)

- [ ] Nama resmi game
- [ ] Tata letak detail Safe Zone (alur garasi → test → gate)
- [ ] Titik/posisi 4 gate + gate ekstraksi (dikunci di gate mana)
- [ ] Jumlah & sebaran kampung kecil, posisi 2 markas faksi
- [ ] Detail heat system: angka pasti (berapa mobil hancur → naik level)
- [ ] Detail blueprint & resep craft (tier senjata/modul)
- [ ] Sistem curi kendaraan (semi-truck) — design pass menyusul
- [ ] Perilaku AI detail (route patrol, aggro radius, kecepatan)

---

# Technical Notes (keselarasan dengan kode yang ada)

## 8. Sistem yang Sudah Ada

- Grid system + save/load JSON (`VehicleGridSystem`, `GridSaveSystem`)
- Database (`VehicleDatabase`, `ModuleDatabase`, `WeaponDatabase`)
- Combat & damage (`KinematicProjectile`, `ExplosionManager`, `VehicleCriticalPart`, `WheelHealth`)
- Turret & HUD (`ManualTurretController`, `VehicleGridWeaponTrigger`, `VehicleHUD`)
- Scene flow (`SceneController` — Garasi 2 index 0, Battlefield index 1)
- `VehicleStatsManager` menghitung stat kendaraan dari `VehicleBaseData`, modul terpasang, dan critical parts (dirty-flag di `LateUpdate`)
- `PlayerController` — FPP dengan Character Controller, switch kamera ke `VehicleCamera` (TPP) saat masuk mobil

## 9. Bug / Isu yang Harus Dibereskan (ditemukan saat audit)

1. **HUD senjata tidak muncul di Battlefield** — `hudContainer: {fileID: 0}` (null) di semua prefab kendaraan; hanya kendaraan spawn runtime yang di-wire `BattlefieldManager`. Fix: set referensi di prefab + fallback auto-find.
2. **`VehicleHUDSpawner` tidak ada di prefab kendaraan** — HUD telemetri tidak pernah muncul.
3. **Salah nama container**: script cari `"HUD_Container"`, scene bernama `"HUD container"`.
4. Container HUD ter-anchor di `(-691,-426)` — potensial di luar layar.
5. (Selesai) Semua `Debug.Log` informasional sudah di-gate `#if UNITY_EDITOR` biar build bersih.

## 10. Persiapan Seamless 4x4 km

- Jaga budget: pooling proyektil sudah ada, tambah **LOD** & **scatter otomatis** untuk batu/semak
- Collider batuan besar disederhanakan
- POI (kampung) ditata tangan; filler diprogram/scatter
- Siang-malam: lighting yang murah untuk URP (lighting bake + emissive)

## 11. Sandstorm Transition (rencana VFX)

- Particle system (3 layer: debu halus, butiran pasir, partikel besar)
- Screen overlay (fade sandy brown + blur, durasi ±3 detik)
- Audio angin kencang fade in
- Camera shake sinusoidal
- Setelah selesai → transisi seamless ke scene berikutnya

## 12. Controls

| Input | Aksi |
|-------|------|
| `WASD` | Gerak (on-foot) / Gas & setir (kendaraan) |
| `Mouse` | Lihat / aim |
| `Space` | Jump (on-foot) |
| `Shift` | Sprint (on-foot) |
| `E` | Masuk/keluar kendaraan |
| `I` | Starter mesin (on/off) |
| `L` | Lampu (on/off) |
| `R` | Reload senjata / rotasi modul saat placement |
| `F` | Interaksi looting (tahan) |
| `X` | Hapus modul yang dipilih |
| `Esc` | Kembali ke Garasi |
| `Klik Kiri` | Tembak (kendaraan) |
| `Klik Kanan` | Batal drag (inventory) |

## 13. Tech Stack

- Unity `2022.3.32f1`
- URP `14.0.11`
- Cinemachine
- TextMesh Pro
- Timeline

## 14. Project Structure

- `Assets/Scripts` — sistem inti gameplay (grid, stat, player, loot zone)
- `Assets/Weapons` — data dan logic senjata
- `Assets/Wheel` — vehicle control dan vehicle data
- `Assets/Scenes` — scene utama (Garasi 2, Battlefield)
- `Assets/JMO Assets/WarFX` — VFX asset (api, ledakan)
- `Assets/TutorialInfo` — readme / tutorial asset bawaan Unity

## 15. How to Run

1. Buka project di Unity `2022.3.32f1`
2. Buka scene `Garasi 2`
3. Atur kendaraan di garasi
4. Jalan ke mobil → tekan `E` untuk naik → `I` untuk starter
5. Menuju border badai pasir untuk masuk Battlefield (saat ini placeholder)

## 16. Key Technical Notes

- `PlacedModule` dan `GridZone` didefinisikan di `VehicleGridSystem.cs`
- `VehicleStatsManager` sekarang hanya koordinator — grid logic ada di `VehicleGridSystem`, runtime fuel/battery masih di VSM
- GridSaveSystem pakai `VehicleGridSystem` parameter, bukan `VehicleStatsManager`
- Semua pre-existing script tetap akses grid via forwarding properties di VSM (`vsm.installedModules`, `vsm.gridZones`, dll)
- Pastikan prefab kendaraan punya component `VehicleGridSystem` + `VehicleStatsManager`

## 17. Credits

Project ini memakai asset kendaraan, texture, audio, dan VFX dari beberapa sumber. Kalau mau dipublish, tambahkan daftar kredit lengkap di bagian ini.
