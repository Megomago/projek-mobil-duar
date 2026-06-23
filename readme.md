# 🚗 VDSO — Vehicle, Drive, Shoot, Shoot Outside
> *"Vidiso"* — Action Shooter Vehicle Game

[![Status](https://img.shields.io/badge/status-in%20development-yellow)]()
[![Platform](https://img.shields.io/badge/platform-PC%20%7C%20Mobile-blue)]()
[![Genre](https://img.shields.io/badge/genre-Action%20Shooter%20Vehicle-green)]()

---

## 📋 Daftar Isi
- [Gambaran Umum](#-gambaran-umum)
- [Sistem Kendaraan (Vehicle Physics)](#-sistem-kendaraan-vehicle-physics)
- [Sistem Senjata Modular](#-sistem-senjata-modular)
- [Sistem Loadout \& Garasi](#-sistem-loadout--garasi)
- [Struktur Scene](#-struktur-scene)

---

## 🎯 Gambaran Umum

**VDSO** (dibaca: Vidiso) adalah game **Action Shooter Vehicle** di mana pemain mengendalikan kendaraan bersenjata. Game ini berfokus pada simulasi fisika kendaraan yang mendalam (advanced vehicle physics) dipadukan dengan sistem persenjataan modular yang kaya akan kustomisasi.

### Ringkasan Cepat
| Kategori | Detail |
|---|---|
| Genre | Action Shooter Vehicle |
| Platform | PC \& Mobile |
| Art Style | 3D Semi-Realistic |

---

## 🚙 Sistem Kendaraan (Vehicle Physics)

Sistem kendaraan dibangun menggunakan *custom controller* berbasis fisika yang mendetail, memberikan keseimbangan antara simulasi realistis dan keseruan aksi *arcade*.

### Fitur Utama Mesin \& Transmisi
- **Simulasi RPM \& Torsi**: Mesin memiliki kurva torsi dinamis, RPM *idle*, dan RPM maksimum (*redline*). Terdapat simulasi inersia *flywheel* untuk transisi RPM yang mulus.
- **Drivetrain Terpisah**: Mendukung konfigurasi penggerak roda FWD (Depan), RWD (Belakang), dan AWD (Semua Roda).
- **Transmisi Manual \& Otomatis**:
  - **Manual**: Pemain dapat memindahkan gigi secara manual lengkap dengan simulasi input kopling.
  - **Otomatis**: Dilengkapi logika cerdas seperti *Auto Upshift*, *Aggressive Downshift* saat pengereman keras (*rev-matching*), dan *Kickdown* saat kendaraan menanjak atau butuh torsi mendadak untuk mencegah mesin mati (*stall*).
- **Arcade Launch Control**: Memungkinkan pemain menahan rem tangan (handbrake) dari posisi diam untuk melakukan *burnout* dan lepas landas dengan RPM tinggi.

### Fisika Suspensi \& Traksi
- **Kustomisasi WheelCollider**: Pengaturan *Spring Rate*, *Damper*, *Suspension Distance*, serta modifikasi kurva gesekan (*Friction*) longitudinal dan lateral secara terpisah.
- **Anti-Roll Bar \& Downforce**: Menjaga stabilitas mobil saat berbelok tajam dan memberikan gaya tekan ke bawah (*downforce*) yang proporsional dengan peningkatan kecepatan mobil.
- **Sistem Pengereman**: Mendukung *brake bias* (distribusi rem depan/belakang) dan rem tangan (*handbrake*) yang terpisah.

---

## 🔫 Sistem Senjata Modular

Setiap kendaraan memiliki *slot* untuk dipasangi berbagai jenis senjata secara modular. Sistem ini didorong oleh arsitektur berbasis Data (`ScriptableObject` / `WeaponData`).

### Mekanisme Menembak \& Balistik
- **Kinematic Projectile \& Object Pooling**: Peluru disimulasikan menggunakan raycast/kinematik proyektil yang di-manage oleh sistem *Object Pooling* untuk menjaga performa game tetap stabil.
- **Parameter Variatif**: Mendukung kecepatan peluru (*muzzle velocity*), *Fire Rate* (RPM), sistem peluru ganda (*Pellet Count* untuk senapan patah/shotgun), dan penyebaran dasar proyektil (*Base Dispersion*).
- **Overheat System**: Senjata dapat mengalami panas berlebih (*overheat*). Semakin panas senjata, level *dispersion* akan dikalikan (*multiplier*), membuat tembakan menjadi sangat tidak akurat sebelum senjata sempat didinginkan.
- **Fisika Hentakan (Recoil)**: Tiap tembakan memberikan gaya dorong fisik (*impulse*) yang akan mengguncang *Rigidbody* kendaraan, diiringi efek getaran layar (*Camera Shake*).
- **Casing Ejection**: Selongsong peluru dilontarkan secara fisik dengan gaya lontar dan putaran acak untuk menambah imersi.

### Aiming \& Turret Controller
- **Manual Turret Controller**: Meriam/Turret membidik secara mandiri dengan menyesuaikan sumbu *Yaw* dan *Pitch* menuju target *crosshair* pemain.
- **Bebas Sumbu Pivot**: Logika aiming menghitung rotasi secara relatif terhadap atap kendaraan, sehingga rotasi arah tembak tidak mengalami *gimbal lock* atau masalah orientasi saat kendaraan berada di bidang miring atau bahkan terbalik.

### Animasi Prosedural
Sistem animasi tidak bergantung penuh pada file *Animator*, melainkan menggerakkan *mesh* secara langsung melalui kode (*procedural*) untuk memberikan kontrol maksimal:
- **Recoil Parts**: Menggerakkan kokangan (*bolt*) atau laras ke belakang dan memantulkannya kembali (*snap and return*) setiap menembak.
- **Rotary Barrel (Minigun)**: Laras memutar secara bertahap (*spin-up*) sebelum mencapai kecepatan rotasi maksimal untuk bisa menembak.
- **Rotatable Parts (Revolver)**: Memutar silinder/sabuk amunisi beberapa derajat dengan sinkronisasi presisi setiap peluru dilontarkan.

---

## 🛠️ Sistem Loadout \& Garasi

Sistem antarmuka di mana pemain dapat memodifikasi kendaraan pertempuran mereka.

- **Vehicle \& Weapon Database**: Keseluruhan daftar mobil dan jenis persenjataan tersimpan di dalam struktur database statis.
- **Sistem Slot Persenjataan**: Tiap varian kendaraan memiliki kapasitas slot senjata yang berbeda untuk menyeimbangkan *gameplay*.
- **Lobby 3D \& UI Grid**: Pemain melihat pratinjau kendaraan mereka secara langsung di dunia 3D. Pemilihan senjata dilakukan melalui sistem *grid* inventaris yang intuitif, lalu disimpan secara otomatis ke dalam konfigurasi lokal (*PlayerPrefs*).

---

## 🗺️ Struktur Scene

- **`Garasi` \& `Garasi 2`**: Scene untuk menu utama, pemilihan kendaraan, dan pemasangan *loadout*. Kendaraan di-spawn tanpa input kendali aktif untuk visualisasi estetis.
- **`Battlefield`**: Arena pertempuran utama di mana sistem fisika kendaraan dan simulasi pertempuran aktif.

---
<div align="center">
  <i>Game Design Document berdasarkan implementasi aktual di Unity.</i>
</div>