***

# VDSO — Vehicle, Drive, Shoot, Shoot Outside

## Prototipe Vehicle Combat Berbasis Sistem Kompleks dan Supply-Driven
*Dikembangkan menggunakan Unity 2022.3 (URP 14)*

---

https://github.com/user-attachments/assets/8c2fbfa8-3dbc-48d2-aeb2-35c17223bf1c



## 1. Gambaran Umum
**VDSO (Vehicle, Drive, Shoot, Shoot Outside)** adalah proyek prototipe *vehicle combat* berbasis sistem kompleks (*systems-driven*) yang berfokus pada logistik, manajemen sumber daya, dan pertempuran taktis tingkat tinggi.

Berbeda dengan game pertempuran kendaraan tradisional, sistem pertempuran dalam VDSO sepenuhnya bersifat **supply-driven** (bergantung pada pasokan). Daya tembak kendaraan dibatasi secara langsung oleh tata letak fisik dan infrastruktur energi kendaraan itu sendiri. Sebagai contoh, memasang senjata tingkat tinggi seperti *Railgun* membutuhkan integrasi bank kapasitor tambahan dan baterai berkapasitas besar. Sementara itu, penggunaan *Flamethrower* membutuhkan modul penyimpanan bahan bakar khusus atau trailer logistik yang ditarik di belakang kendaraan.

Proyek ini mengintegrasikan **Custom Vehicle Physics**, **3D Diegetic Grid Inventory (ala Resident Evil 4)**, dan arsitektur kode **Zero-Allocation** untuk performa balistik yang optimal.

---

## 2. Core Game Loop
Alur permainan dirancang di sekitar perencanaan sumber daya, eksekusi taktis, dan adaptabilitas di lapangan:

```
Garasi (Perencanaan Sumber Daya & Loadout Grid 3D) 
   └── Medan Tempur (Combat Sandbox & Simulasi Berkendara)
         └── Keterlibatan Taktis (Membajak Kendaraan / Operasi Turret On-foot)
               └── Eksfiltrasi (Menyimpan Loadout via PlayerPrefs)
```

---

## 3. Arsitektur Sistem Pertempuran & Balistik

### Pipeline Senjata Modular
Persenjataan kendaraan didefinisikan sebagai `WeaponData` (*ScriptableObjects*) dan dieksekusi saat runtime melalui `ModularWeapon.cs`. Arsitektur yang terdekopel (*decoupled*) ini memungkinkan iterasi konten senjata tanpa perlu mengubah kode inti.

*   **Zero-Allocation Ballistics:** Trajektori proyektil disimulasikan menggunakan *Kinematic Projectile* dengan metode *Raycast Sub-stepping*. Sistem *Object Pooling* diterapkan secara ketat untuk proyektil, selongsong (*casing*), *muzzle flash*, dan efek dampak (*VFX Impact*) guna meminimalisir lonjakan *Garbage Collection*.
*   **Distribusi Pellet Gaussian:** Sebaran peluru jenis *shotgun* atau *shrapnel* menggunakan model distribusi Gaussian murni yang diatur oleh parameter *Choke* dinamis.
*   **Manajemen Termal (Overheat):** Penembakan secara konstan akan meningkatkan akumulasi panas. Mendekati batas termal maksimum akan meningkatkan dispersi dasar senjata secara eksponensial hingga siklus pendinginan selesai.

*   https://github.com/user-attachments/assets/701ab109-da9d-4aab-a8a7-665468cfac92

### Matematika Penetrasi & Logika Armor
Prototipe ini meninggalkan sistem pengurangan *health-bar* konvensional dan beralih ke model penetrasi armor deterministik:

```
Jika (PEN > DEF):
    Damage Diterima = (ATK + DEF) - HP
    Proyektil mempertahankan sisa penetrasi berdasarkan persentase (PEN - DEF) relatif terhadap PEN awal.
```
*Senjata kinetik dengan penetrasi tinggi (seperti APDS) dirancang untuk menembus pelat luar guna merusak komponen internal yang kritis secara langsung.*

---

## 4. Fisika Kendaraan & Sistem Grid

### Custom Hybrid Vehicle Controller
Model berkendara menjembatani responsivitas kontrol *arcade* dengan parameter simulasi fisik yang realistis:
*   **Mesin & Drivetrain:** Menyediakan simulasi kurva torsi dinamis, inersia *flywheel*, RPM *idle*, dan *rev-limiter*. Mendukung konfigurasi FWD, RWD, dan AWD.
*   **Logika Transmisi:** Transmisi manual dilengkapi dengan simulasi input kopling. Transmisi otomatis dilengkapi dengan fitur *proactive upshifting*, *aggressive downshifting* saat pengereman keras, dan algoritma *kickdown* untuk mencegah mesin mati (*stall*) di tanjakan curam.
*   **Dinamika Sasis:** Sistem suspensi yang dapat disesuaikan per roda (*spring rates*, *dampening*, *suspension travel*, *anti-roll bars*, dan *downforce* proporsional terhadap kecepatan).
*   **Transfer Recoil:** Gaya dorong mundur (*recoil*) dari senjata dihitung secara fisik dan diterapkan sebagai vektor impuls negatif langsung ke komponen *Rigidbody* kendaraan.
*   https://github.com/user-attachments/assets/a7c98ed4-9867-477c-919f-eeab241ad898

### Loadout Grid 3D Diegetik
Kendaraan dibatasi oleh tata letak grid fisik dan batas berat dasar (*Base Weight*) yang ketat.
*   **Slot Internal:** Pemain harus menyeimbangkan tata letak grid antara persenjataan dan sumber daya penting (Baterai, Panel Surya, Sel Bahan Bakar, Rak Amunisi).
*   **Umpan Balik Fisik:** Menambahkan modul akan meningkatkan total massa kendaraan, yang secara dinamis mengubah akselerasi, jarak pengereman, dan pusat gravitasi (*center of mass*).

---

## 5. Modul Logistik Towing (Sistem Joint Physics)
Untuk memperpanjang durasi pertempuran, pemain dapat memasang trailer fisik menggunakan sistem sambungan dinamis.
*   **Trailer Modular:** Opsi yang tersedia mencakup Generator Portabel (Genset), Tangki Bahan Bakar, atau artileri berat (Meriam Anti-Tank 75mm).
*   **Logika Pelepasan:** Trailer beroperasi sebagai entitas fisik terpisah dengan nilai HP dan DEF mandiri. Pemain dapat memicu pelepasan darurat saat runtime (memutuskan sambungan `HingeJoint`) untuk menukar pasokan energi dengan pengurangan berat instan demi mobilitas.

---

## 6. Transmisi On-Foot & Kontrol Turret Eksternal
Untuk memaksimalkan kedalaman taktis, prototipe ini memfasilitasi transisi antara pengoperasian kendaraan dan mekanik infanteri di luar kendaraan (*on-foot*).

*   **Mode Infanteri:** Pemain dapat keluar dari kendaraan pada area koordinat tertentu. Untuk menjaga performa dan menghindari *bug* fisik (*clipping*), kontrol infanteri menggunakan model pergantian status/teleportasi cepat untuk mengalihkan kendali pemain antara kendaraan dan karakter.
*   **Mekanik Pembajakan:** Pemain yang berada di luar kendaraan dapat mendekati dan membajak kendaraan musuh secara langsung.
*   **Senjata Eksternal:** Pemain dapat menggunakan senjata tipe *External Control* secara manual (misalnya, senapan mesin DShK di atas bak kendaraan utilitas). Senjata-senjata ini beroperasi secara independen dari sistem kontrol utama kendaraan, berfungsi sebagai posisi pertahanan statis.

---

## 7. Status Proyek & Rencana Pengembangan (Roadmap)

### Sistem yang Sudah Diimplementasikan
*   *Core loop* yang dapat dimainkan (Garasi $\rightarrow$ Spawning medan tempur $\rightarrow$ Pertempuran $\rightarrow$ Persistensi data).
*   *Custom vehicle controller* hybrid dengan opsi transmisi manual/otomatis.
*   Sistem penyimpanan *loadout* 3D dengan persistensi data via PlayerPrefs.
*   Verifikasi arsitektur senjata selesai (HVAP30 Mark. I) dengan VFX kustom, audio, dan sekuens *reload*.

### Rencana Pengembangan & Backlog
| Sistem | Status Implementasi |
| :--- | :--- |
| **Kerusakan Komponen (Mesin/Ban)** | Direncanakan (Struktur awal tersedia di `KinematicProjectile.cs`) |
| **AI Perilaku Musuh (Threat Behavior)** | Direncanakan |
| **UI Grid Snapping 3D** | Dalam Pengembangan |
| **Kalibrasi Stabilitas Towing Joint** | Dalam Tahap Pengujian |

---

## 8. Spesifikasi Teknis & Referensi Script

*   **Versi Engine:** Unity 2022.3 (URP 14)
*   **Target Platform:** PC / Mobile (Termasuk varian VFX yang dioptimalkan untuk perangkat mobile)

### Direktori Script Utama

| Class / Komponen | Path File | Deskripsi |
| :--- | :--- | :--- |
| `VehicleController` | `Assets/Wheel/VehicleController.cs` | Kontrol fisik kustom, kurva torsi, dan logika transmisi. |
| `ModularWeapon` | `Assets/Weapons/ModularWeapon.cs` | Eksekusi parameter senjata dan sistem *pooling*. |
| `ManualTurretController` | `Assets/Weapons/ManualTurretController.cs` | Perhitungan rotasi *aiming slerp* untuk mencegah *Gimbal Lock*. |
| `LoadoutManager` | `Assets/Weapons/LoadoutManager.cs` | Mengelola tata letak grid persisten dan kueri database lokal. |

---

## 9. Kolaborasi & Pengembangan
Proyek ini saat ini dikembangkan secara mandiri oleh seorang **Systems Designer**.

Saya membuka kesempatan kolaborasi aktif untuk peran berikut:
1.  **Gameplay/AI Programmer:** Untuk mengembangkan kecerdasan buatan (*AI enemy*) yang dapat bereaksi secara dinamis terhadap keterbatasan sumber daya (panas senjata, manajemen energi).
2.  **Physics Engineer:** Untuk mengoptimalkan batasan fisik sambungan (*joint constraints*) pada mekanik penarikan (*towing*) berkecepatan tinggi.

Untuk pertanyaan teknis atau proposal kolaborasi, silakan buka *Issue* atau kirimkan *Pull Request* pada repositori ini.

***
