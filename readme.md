# VDSO — Vehicle, Drive, Shoot, Shoot Outside

> Action Shooter Vehicle Game (Unity 2022.3, URP)

[![Status](https://img.shields.io/badge/status-in%20development-yellow)]()
[![Platform](https://img.shields.io/badge/platform-PC%20%7C%20Mobile-blue)]()
[![Genre](https://img.shields.io/badge/genre-Action%20Shooter%20Vehicle-green)]()

---

## Daftar Isi

- [Gambaran Umum](#gambaran-umum)
- [Core Loop Pemain](#core-loop-pemain)
- [Sistem Kendaraan](#sistem-kendaraan)
- [Sistem Senjata Modular](#sistem-senjata-modular)
- [Sistem Loadout dan Garasi](#sistem-loadout-dan-garasi)
- [Struktur Scene](#struktur-scene)
- [Konten dan Cakupan Proyek](#konten-dan-cakupan-proyek)
- [Pengembangan Selanjutnya](#pengembangan-selanjutnya)
- [Referensi](#referensi)

---

## Gambaran Umum

**VDSO** (Vehicle, Drive, Shoot, Shoot Outside) adalah proyek game **Action Shooter Vehicle** di mana pemain mengendalikan kendaraan bersenjata, mengatur konfigurasi persenjataan di garasi, lalu bertempur di arena terbuka.

Proyek ini menggabungkan simulasi fisika kendaraan dengan sistem persenjataan modular berbasis data. Arsitektur dirancang agar konten senjata dan kendaraan dapat ditambahkan tanpa perubahan kode inti.

| Kategori | Detail |
|---|---|
| Genre | Action Shooter Vehicle |
| Platform | PC & Mobile |
| Art Style | 3D Semi-Realistic |
| Engine | Unity 2022.3 (URP 14) |
| Status | Prototype — core loop playable |

---

## Core Loop Pemain

Alur permainan saat ini mengikuti siklus berikut:

```
Garasi → Pilih kendaraan → Pasang senjata pada slot → Battlefield → Berkendara & menembak → Kembali ke garasi
```

| Tahap | Scene | Fungsi |
|---|---|---|
| Hub / Loadout | `Garasi 2` | Pemilihan kendaraan, konfigurasi senjata, pratinjau 3D |
| Combat Sandbox | `Battlefield` | Simulasi berkendara, aiming, dan pertempuran |
| Transisi | — | Konfigurasi loadout disimpan lokal via `PlayerPrefs` |

---

## Sistem Kendaraan

Sistem kendaraan menggunakan custom controller berbasis fisika dengan pendekatan hybrid antara simulasi dan respons arcade.

### Mesin dan Transmisi

- **Simulasi RPM dan Torsi** — Kurva torsi dinamis, RPM idle, redline, dan inersia flywheel.
- **Drivetrain** — Konfigurasi FWD, RWD, dan AWD.
- **Transmisi Manual** — Perpindahan gigi manual dengan simulasi input kopling.
- **Transmisi Otomatis** — Auto upshift, aggressive downshift saat pengereman, dan kickdown untuk mencegah stall di medan menanjak.
- **Launch Control** — Handbrake dari posisi diam memungkinkan burnout dan lepas landas dengan RPM tinggi.

### Suspensi, Traksi, dan Handling

- Kustomisasi per roda: spring rate, damper, suspension travel, dan kurva gesekan longitudinal/lateral.
- Anti-roll bar per axle dan downforce proporsional terhadap kecepatan.
- Brake bias dan handbrake terpisah pada roda belakang.
- Pengurangan sudut stereng berdasarkan kecepatan.

### Integrasi dengan Sistem Pertempuran

- Gaya recoil senjata diterapkan ke rigidbody kendaraan.
- Camera shake saat menembak, skala disesuaikan per senjata.
- Input Battlefield: WASD (berkendara), Space (handbrake), LMB (menembak), R (reload), mouse (aiming), ESC (kembali ke garasi).

---

## Sistem Senjata Modular

Setiap kendaraan memiliki slot persenjataan yang dapat diisi secara modular. Seluruh parameter senjata didefinisikan melalui `WeaponData` (ScriptableObject) dan dieksekusi oleh `ModularWeapon` pada runtime.

### Balistik dan Performa

- **Kinematic Projectile** — Simulasi peluru dengan raycast sub-stepping; object pooling untuk projectile, casing, muzzle flash, dan impact VFX.
- **Parameter Tembak** — Muzzle velocity, fire rate (RPM), pellet count (shotgun), base dispersion, dan choke multiplier.
- **Overheat** — Akumulasi panas per tembakan; dispersi meningkat saat suhu mendekati batas maksimum.
- **Recoil** — Impulse fisik pada kendaraan dan camera shake per tembakan.
- **Casing Ejection** — Selongsong peluru dilontarkan dengan gaya dan rotasi acak.

### Aiming

- **Manual Turret Controller** — Rotasi yaw/pitch menuju crosshair pemain.
- **Orientasi Relatif Kendaraan** — Perhitungan aiming menggunakan sumbu atap kendaraan untuk menghindari gimbal lock pada medan miring atau orientasi tidak standar.

### Animasi Prosedural

Animasi senjata dijalankan sebagian melalui kode, terpisah dari Animator:

| Tipe | Perilaku |
|---|---|
| Recoil Parts | Bolt/laras bergerak mundur dan kembali setiap tembakan |
| Rotary Barrel | Spin-up sebelum fire rate penuh (minigun) |
| Rotatable Parts | Rotasi silinder/sabuk amunisi per tembakan (revolver) |

Reload dapat dikombinasikan dengan Animator (`ReloadAnim`) dan efek fisik magazine drop.

### Contoh Konten: HVAP30 Mark. I

| Parameter | Nilai |
|---|---|
| Muzzle velocity | 950 m/s |
| Fire rate | 800 RPM |
| Kapasitas magazine | 400 |
| Auto-reload | 4,5 detik |
| Procedural recoil | Aktif |

Asset: `Assets/Senjata/HVAP30 Mk i/30 mm HVAP.asset`

---

## Sistem Loadout dan Garasi

### Database

- **VehicleDatabase** — Daftar kendaraan playable beserta prefab dan jumlah slot senjata.
- **WeaponDatabase** — Daftar senjata yang tersedia di grid picker.

### Balance antar Kendaraan

| Kendaraan | Slot Senjata |
|---|---|
| Sedan AK | 2 |
| Sedan EF | 1 |

Perbedaan jumlah slot berfungsi sebagai lever balance tanpa memerlukan stat sheet terpisah.

### Alur UI Garasi

1. Navigasi antar kendaraan (Prev/Next); pilihan disimpan via `PlayerPrefs`.
2. Pratinjau kendaraan 3D — fisika aktif, kontrol dan audio dinonaktifkan.
3. Mode inventaris → pemilihan slot → grid senjata (termasuk opsi melepas senjata).
4. Konfigurasi per slot disimpan per kendaraan: `WeaponSlot_{vehicleName}_{slotIndex}`.

### UX Tambahan

- Kamera garasi (Cinemachine FreeLook) dengan orbit manual (klik kanan) dan auto-rotate setelah idle.
- HUD senjata per slot di Battlefield: ammo, timer reload, prompt input.

---

## Struktur Scene

| Scene | Build Index | Fungsi |
|---|---|---|
| `Garasi 2` | 0 | Entry point — garasi, loadout, transisi ke Battlefield |
| `Battlefield` | 1 | Arena combat sandbox |
| `Garasi` | — | Varian garasi legacy, tidak digunakan di build |

---

## Konten dan Cakupan Proyek

### Yang Sudah Diimplementasikan

- Core loop playable: garasi → loadout → battlefield → kembali.
- Custom vehicle physics dengan transmisi manual/otomatis.
- Pipeline senjata modular berbasis ScriptableObject.
- Sistem loadout dengan persistensi lokal.
- Satu senjata fully authored (HVAP30 Mark. I) beserta VFX, audio, dan animasi reload.
- Dua varian kendaraan dengan konfigurasi slot berbeda.
- Environment garasi custom dan arena Battlefield (terrain-based test map).
- Optimasi performa: object pooling, kinematic projectile, varian VFX mobile.

### Yang Belum Diimplementasikan

| Sistem | Status |
|---|---|
| AI / musuh | Belum ada |
| Damage system (sasis, mesin, roda) | Direncanakan — referensi TODO di `KinematicProjectile.cs` |
| Misi dan objective | Belum ada |
| Progression dan unlock | Belum ada |
| Economy | Belum ada |
| Multiplayer | Belum ada |
| Level design final | Battlefield masih berfungsi sebagai test arena |

---

## Pengembangan Selanjutnya

Prioritas desain yang direncanakan berdasarkan arsitektur saat ini:

1. **Modular Damage System** — Kerusakan terlokalisasi pada komponen kendaraan (sasis, mesin, roda).
2. **Konten Senjata Tambahan** — Memvalidasi pipeline modular dengan tipe senjata berbeda (shotgun, minigun, dll.).
3. **AI Musuh** — `ManualTurretController` sudah mendukung `SetAimTarget()` untuk integrasi AI.
4. **Struktur Misi** — Objective-based gameplay di atas core loop yang ada.
5. **Playtest Mobile** — Verifikasi performa pooling dan VFX pada perangkat target.

---

## Referensi

### Alat Desain

- [Balanctool v2 Simulator](https://balanctoolv2.netlify.app/) — Simulasi balancing senjata/kendaraan

### Script Utama

| Sistem | Path |
|---|---|
| Vehicle controller | `Assets/Wheel/VehicleController.cs` |
| Weapon data | `Assets/Weapons/WeaponData.cs` |
| Weapon runtime | `Assets/Weapons/ModularWeapon.cs` |
| Loadout manager | `Assets/Weapons/LoadoutManager.cs` |
| Battlefield spawn | `Assets/Scripts/BattlefieldManager.cs` |
| Turret aiming | `Assets/Weapons/ManualTurretController.cs` |
| Vehicle database | `Assets/Pre mobil/Kendaraan.asset` |
| Weapon database | `Assets/Weapons/WeaponDatabase.asset` |

---

<div align="center">
  <i>Dokumen ini mencerminkan implementasi aktual di Unity dan cakupan proyek saat ini.</i>
</div>
