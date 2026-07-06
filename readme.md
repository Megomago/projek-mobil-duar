# Projek Mobil Duuaar

Game ekstraksi kendaraan modular berbasis Unity. Build kendaraan, pasang modul, lalu terjun ke zona ekstraksi seluas 4x4 km buat cari loot, bertahan dari pengejaran musuh, dan kabur.

## 1. Game Overview

Game ini adalah extraction shooter dengan kendaraan sebagai fokus utama. Pemain mulai di garasi, merakit kendaraan dari modul-modul yang dipasang di grid, lalu memasuki zona ekstraksi untuk mengumpulkan barang berharga. Sistem pengejaran musuh ala NFS Most Wanted — makin lama di zona, makin agresif pengejar. Bisa turun dari kendaraan untuk looting di dalam gedung, tapi combat utama tetap vehicle-to-vehicle.

## 2. Core Game Loop

1. Masuk garasi, pilih kendaraan
2. Buka inventory, pasang/lepas modul di grid
3. Simpan konfigurasi kendaraan
4. Spawn di test area (garasi scene, 200x200m)
5. Jalan kaki ke mobil, masuk, hidupkan mesin
6. Uji coba kendaraan — kalau oke, melaju ke border
7. Border 200m → badai pasir (VFX transition + loading)
8. Masuk Extraction Zone (scene terpisah, 4x4 km)
9. Cari loot di dalam gedung (turun mobil, FPP)
10. Hadapi pengejaran musuh (makin lama makin susah)
11. Temukan titik extract dan kabur
12. Balik ke garasi, upgrade dari hasil loot

## 3. Game Flow

### Garasi Scene (200x200m)

- Mode preview kendaraan
- Inventory modul + placement di grid zona
- Simulasi stat kendaraan dari komposisi modul
- Player spawn jalan kaki (FPP) — bisa langsung naik mobil
- Area test terbatas 200x200m
- Border berupa tembok badai pasir visual
- Pas menembus badai → VFX badai gurun (3 detik) + loading ke scene extraction

### Extraction Zone (4x4 km)

- Open world dengan bangunan yang bisa dimasuki
- Loot tersebar di dalam rumah/gedung
- Musuh mengejar menggunakan kendaraan (sistem NFS Most Wanted)
- Heat level meningkat seiring waktu → makin banyak + agresif pengejar
- Titik ekstraksi tersebar di peta
- Setelah extract → balik ke garasi

### On-Foot Mode (FPP)

- Hanya untuk looting di dalam gedung, bukan untuk combat
- Tidak ada senjata on-foot khusus (cukup sidearm/melee sederhana)
- Looting pakai progress bar (tahan F) — tanpa animasi tangan visible
- Sistem inventory dengan weight limit (max carry load)
- Mode sneak (jalan pelan, engine mati) biar musuh lewat
- Pas turun dari kendaraan: `E` di dalam kendaraan → exit di exit point

### Sistem Pengejaran (NFS Most Wanted Style)

| Heat Level | Musuh | Perilaku |
|------------|-------|----------|
| 1 | 1-2 pengejar ringan | Ngejar, tabrakan ringan |
| 2 | 2-3 pengejar medium | Ngejar + tembakan ringan |
| 3 | 3-4 pengejar berat | Ngejar + senjata berat |
| 4 | 4+ pengejar elite | Agresif, block jalur, jebakan |

## 4. Loot System — 2 Layer Propagation

### Fixed Loot (Blueprint, Kendaraan Parkir)
- Posisi tetap, spawn tiap masuk scene
- Punya `uniqueId` — sekali diambil, ilang permanen (disimpan di PlayerPrefs)
- Cocok untuk item spesial: blueprint senjata, kendaraan baru, modul rare

### Random Loot (Baut, Scrap, Barrel, dll)
- Dispawn per `LootZone` — area trigger di dalam gedung
- Item muncul di permukaan datar (meja, lantai, rak) via raycast ke bawah
- Tiap zone punya `tier` (1-5) + `maxItems`
- LootTable dengan weighted random biar variasi
- Proximity spawn: item cuma dispwan dalam radius ~200m dari player (sisanya pool)

```csharp
public class LootZone : MonoBehaviour
{
    public int tier = 1;
    public int maxItems = 5;
    public float spawnRadius = 1f;
    public LayerMask surfaceMask;  // Floor, Table, Shelf
}
```

## 5. Main Systems

### Vehicle Grid System

`VehicleGridSystem` handle pemasangan modul di zona kendaraan, cek area kosong, rotasi modul, clearance antar modul, dan spawn prefab modul ke posisi yang tepat.

### Vehicle Stats

`VehicleStatsManager` menghitung stat kendaraan dari:

- `VehicleBaseData`
- modul yang terpasang
- critical parts di prefab kendaraan

Stat yang dihitung mencakup massa, armor, power consumption, power generation, battery capacity, fuel capacity, capacitor, dan ammo pool. Stat dirty-flag di `LateUpdate`.

### Loadout / Inventory

`LoadoutManager` ngurus pemilihan kendaraan di garasi, spawn preview kendaraan, buka inventory, dan isi katalog modul dari `ModuleDatabase`.

### Battlefield Spawn

`BattlefieldManager` spawn player dan kendaraan di scene battlefield, lalu load konfigurasi grid yang sudah disimpan.

### Weapons

Weapon disimpan sebagai `WeaponData` dan katalog senjata ada di `WeaponDatabase`. Modul senjata bisa dipasang lewat sistem grid, lalu memakai prefab 3D senjata yang sesuai. ModularWeapon handle firing, overheat, recoil, rotary barrel, reload, ammo pool consumption dari kendaraan.

### Sandstorm Transition

Saat player menembus border 200m di garasi:
- Particle system (3 layer: debu halus, butiran pasir, partikel besar)
- Screen overlay (fade sandy brown + blur, durasi 3 detik)
- Audio angin kencang fade in
- Camera shake sinusoidal
- Setelah selesai → `SceneManager.LoadScene("Extraction")`

### Player Controller

`PlayerController` — FPP (First Person Perspective) dengan Character Controller:
- Mouse look (yaw/pitch)
- WASD movement + sprint (Shift) + jump (Space)
- Kamera FPP di posisi mata (1.6m)
- Pas masuk mobil: player di-disable, kamera switch ke VehicleCamera (TPP)
- Pas turun: player di-enable di exit point

## 6. Feature Highlights

- Modular vehicle building dengan grid zone system (clearance, rotasi)
- Extraction gameplay (loot → survive → extract)
- Loot propagation 2 layer (fixed + random weighted)
- Sistem pengejaran NFS Most Wanted (heat level)
- Sandstorm VFX transisi seamless antar scene
- FPP on-foot mode (looting di interior gedung)
- Save / load loadout kendaraan
- Vehicle stat recalculation otomatis (dirty flag)
- Ammo, fuel, battery, dan capacitor tracking
- Damage ke modul dan destruction + chain explosion

## 7. Scenes

- `Garasi 2` — build / preview / test area (200x200m) + VFX sandstorm border
- `Extraction` — open world 4x4 km, loot, pengejaran (rencana, belum diimplementasi)

## 8. Controls

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

## 9. Tech Stack

- Unity `2022.3.32f1`
- URP `14.0.11`
- Cinemachine
- TextMesh Pro
- Timeline

## 10. Project Structure

- `Assets/Scripts` — sistem inti gameplay (grid, stat, player, loot zone)
- `Assets/Weapons` — data dan logic senjata
- `Assets/Wheel` — vehicle control dan vehicle data
- `Assets/Scenes` — scene utama
- `Assets/JMO Assets/WarFX` — VFX asset (api, ledakan)
- `Assets/TutorialInfo` — readme / tutorial asset bawaan Unity

## 11. How to Run

1. Buka project di Unity `2022.3.32f1`
2. Buka scene `Garasi 2`
3. Atur kendaraan di garasi
4. Jalan ke mobil → tekan `E` untuk naik → `I` untuk starter
5. Menuju border badai pasir untuk masuk Extraction zone (saat ini placeholder)

## 12. Key Technical Notes

- `PlacedModule` dan `GridZone` didefinisikan di `VehicleGridSystem.cs`
- `VehicleStatsManager` sekarang hanya koordinator — grid logic ada di `VehicleGridSystem`, runtime fuel/battery masih di VSM
- GridSaveSystem pakai `VehicleGridSystem` parameter, bukan `VehicleStatsManager`
- Semua pre-existing script tetap akses grid via forwarding properties di VSM (`vsm.installedModules`, `vsm.gridZones`, dll)
- Pastikan prefab kendaraan punya component `VehicleGridSystem` + `VehicleStatsManager`

## 13. Credits

Project ini memakai asset kendaraan, texture, audio, dan VFX dari beberapa sumber. Kalau mau dipublish, tambahkan daftar kredit lengkap di bagian ini.
