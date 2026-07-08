# Dokumentasi Projek Mobil Duuaar

## Architecture Overview

Game ekstraksi kendaraan modular berbasis Unity URP.
Stack: Unity 2022.3.32f1, Cinemachine, TextMesh Pro.

### Scene Flow

```
Garasi 2 (build/test 200x200m)
  ├── Preview kendaraan + inventory grid
  ├── Test drive area (200m border → sandstorm VFX)
  └── Extraction Zone (4x4 km — rencana)
```

---

## Folder Structure

```
Assets/Scripts/
  Vehicle/     VehicleController, VehicleCamera, EngineAudio, WheelEffects,
               VehicleData, VehicleDatabase, VehicleBaseData
  Grid/        VehicleGridSystem, GridVisualizer, GridSaveSystem
  Weapons/     ModularWeapon, WeaponData/DB, ObjectPool, KinematicProjectile,
               ManualTurretController, TracerVisual, ReloadAnim,
               MuzzleFlashLight, MuzzleFlashFX, CircularBuffer
  Modules/     ModuleTemplate/DB, ModuleSelectionManager, ModuleGizmoVisualizer,
               VehicleModuleComponent
  Combat/      ExplosionManager, HitboxProxy, WheelHealth, VehicleCriticalPart,
               VehicleHitboxInitializer, SimpleTarget, VehicleGridWeaponTrigger
  UI/          VehicleHUD, VehicleUIManager, VehicleHUDSpawner, WeaponUIManager,
               LoadoutManager, VehicleModuleListUI, UIModuleItem
  Player/      PlayerController, VehicleEntry
  Scene/       SceneController, BattlefieldManager, Lobbycamera
  Inventory/   InventoryDragDropManager, OptFormula, VehicleStatsManager
```

---

## 🚫 GUARD RAIL — JANGAN DIGANGGU

### 1. CURSOR BEHAVIOR — JANGAN DIUBAH

Cursor management HANYA di 2 tempat:

| File | Method | Perilaku |
|------|--------|----------|
| `VehicleCamera.cs` | `Update()` | **Lock tiap frame**. Alt → unlock. **JANGAN** tambah `if (target == null) return;` |
| `SceneController.cs` | `GoToGarasi()` | Unlock sebelum load scene |
| `VehicleEntry.cs` | `EnterVehicle()` | Lock (backup, redundancy) |
| `VehicleEntry.cs` | `ExitVehicle()` | Unlock (backup, redundancy) |

**Aturan:**
- `VehicleCamera.Update()` HARUS selalu jalan dan lock cursor tiap frame.
- Jangan tahan cursor lock dengan `if (target == null)`.
- Jangan tambah cursor logic di script lain (PlayerController, LoadoutManager, dll).

### 2. SAVE SYSTEM — JANGAN DIUBAH

`GridSaveSystem.cs` pakai file-based save di `Application.persistentDataPath`.

- **JANGAN balik ke PlayerPrefs.**
- Format JSON via `JsonUtility`. Jangan ganti serializer.
- File naming: `Grid_{vehicleName}.json`.
- `LoadGrid` (sync) dan `LoadGridAsync` (coroutine) keduanya HARUS ada.

### 3. DATA MODEL — JANGAN UBAH FIELD NAMA

| Asset | Field | Wajib diisi? |
|-------|-------|--------------|
| `VehicleData.vehicleName` | Nama kendaraan | **YA.** Harus sama dengan `VehicleBaseData.vehicleName` |
| `WeaponData.weaponName` | Nama senjata | YA |
| `ModuleTemplate.moduleName` | Nama modul | YA |

**Aturan:**
- Jangan hapus field `vehicleName` dari `VehicleData`.
- Nama lookup pake dictionary (O(1)), bukan linear scan + GetComponent.
- Jangan balikin GetComponent ke database lookup logic.

### 4. OBJECT POOL — JANGAN DIUBAH

`ObjectPool.cs` singleton. Method:

| Method | Notes |
|--------|-------|
| `Spawn(prefab, pos, rot)` | Return `GameObject`. Bisa null. **HARUS ada null check setelah panggil.** |
| `Despawn(obj)` | Return ke pool |
| `Despawn(obj, delay)` | Delayed return |
| `Warmup(prefab, count)` | Pre-instantiate di loading |

**Aturan:**
- `Spawn()` result **wajib di-null-check** sebelum dipake.
- Jangan ganti `Queue` back ke dynamic allocation.
- `instanceToPrefabMap` HARUS sinkron dengan pool state.

### 5. WEAPON FIRING — JANGAN DIUBAH

`ModularWeapon.cs`:

- `_pendingCasings` → `CircularBuffer` (jangan Queue<float>)
- `RotatablePart.pendingRotationTimes` → `CircularBuffer` (jangan Queue<float>)
- `EjectCasing()` → variable `casingRb`, jangan pake `casing`
- `Fire()` → `heatFactor` udah dihapus, jangan tambah balik
- Muzzle flash spawn → **wajib null check** setelah `ObjectPool.Instance.Spawn()`

### 6. WEAPON TRIGGER — JANGAN DIUBAH

`VehicleGridWeaponTrigger.Update()`:

```csharp
if (InventoryDragDropManager.Instance != null) return;
```

Ini **INTENTIONAL**. Jangan dihapus. Fungsinya:
- Di garage: block tembakan karena player jalan kaki
- Di battlefield: `InventoryDragDropManager` gak ada di scene, jadi gak nge-block
- Bukan bug. Udah diverifikasi.

### 7. TURRET BEHAVIOR — JANGAN DIUBAH

| Skenario | Turret state |
|----------|-------------|
| Battlefield spawn (on foot) | `enabled = false` |
| Masuk mobil | `enabled = true` via `EnableTurrets(true)` |
| Keluar mobil | `enabled = false` via `EnableTurrets(false)` |
| Async load selesai | Di-disable ulang via callback |

**Aturan:**
- Jangan tambah logic turret di script lain.
- `ManualTurretController.enabled = false` cukup buat matiin turret.
- Jangan pake `usePlayerInput` buat ganti-ganti — `enabled` aja.

### 8. INVENTORY GRID — JANGAN DIUBAH

| Method | Notes |
|--------|-------|
| `GetOccupiedCells(pos, w, h, angle, dest)` | Clear + fill `dest`. **Zero alloc.** |
| `GetClearanceCells(pos, template, angle, dest)` | Clear + fill `dest`. **Zero alloc.** |
| `IsAreaFree()` | Pake `HashSet` + `List`. Jangan balik ke O(n²). |

**Aturan:**
- GridVisualizer pake `GridKey` struct, jangan string key.
- Jangan tambah `new List<Vector2Int>()` di `UpdateGridColors()`.
- GridSaveSystem pake `ModuleTemplate.moduleName` sebagai key lookup.

### 9. STATS MANAGER — JANGAN DIUBAH

| Field | Cache di |
|-------|----------|
| `Rigidbody` | `rb` di Awake |
| `VehicleController` | `_vc` di Awake (JANGAN panggil `GetComponent` di Update) |

---

## Key Design Patterns

### Singleton whitelist

| Singleton | File | Notes |
|-----------|------|-------|
| `VehicleCamera.Instance` | VehicleCamera.cs | **WAJIB** ada. Dipanggil banyak script. |
| `ObjectPool.Instance` | ObjectPool.cs | **WAJIB** ada. Pooling system. |
| `InventoryDragDropManager.Instance` | InventoryDragDropManager.cs | Hanya exist di garage scene. Null-safe. |

### CircularBuffer (instead of Queue<float>)

```
CircularBuffer(float[] + head + tail)
├── Enqueue() — O(1), no alloc
├── Dequeue() — O(1), no alloc
├── Peek() — O(1), no alloc
└── Clear() — O(1), no alloc
```

Sudah menggantikan `Queue<float>` di `ModularWeapon._pendingCasings` dan `RotatablePart.pendingRotationTimes`. Capacity 32 cukup untuk semua kasus.

### Object Pooling pattern

```csharp
GameObject obj = ObjectPool.Instance.Spawn(prefab, pos, rot);
if (obj != null)  // ← WAJIB
{
    // use obj
    ObjectPool.Instance.Despawn(obj, delay);
}
```

### Static NonAlloc buffers

| File | Buffer | Size |
|------|--------|------|
| `ExplosionManager._overlapCache` | Collider[] | 64 |
| `KinematicProjectile._raycastHitsBuffer` | RaycastHit[] | 32 |
| `ManualTurretController._turretRaycastBuffer` | RaycastHit[] | 32 |

### Spread LUT (static, shared)

```csharp
_magLUT[1024]  // Box-Muller magnitude, clamped to 3f
_dirLUT[1024]  // Random angle cos/sin
```

Static. Initialize sekali di `InitializeSpreadLUT()`. Jangan regenerate.

---

## Common Pitfalls

| Jangan lakukan | Akibat |
|----------------|--------|
| Panggil `Camera.main` di Update/LateUpdate | `FindGameObjectsWithTag` tiap frame — slow. Cache di Awake. |
| Panggil `GetComponent<VehicleController>()` di Update | Cache `_vc` di Awake. |
| Panggil `GetComponentsInChildren<Light>()` tiap frame | Cache `_cachedLights` di Awake (`VehicleCriticalPart`). |
| Pake `new List<Vector2Int>()` di Update | Cache sebagai field (`readonly List`). |
| Pake `Queue<float>` untuk timing | Pake `CircularBuffer` (pre-allocated, zero GC). |
| String concatenation untuk dictionary key (`$"{zone}_{x}_{y}"`) | Pake `GridKey` struct. |
| Lewati null check setelah `ObjectPool.Instance.Spawn()` | NRE. Pool bisa return null. |
| Ubah `heatFactor` variable atau hardcoded `threshold = 0.8f` | Udah dihapus. Pake `weaponData.overheatDispersionThreshold`. |
| Balikin `HasIntersection` ke `List.Contains` | O(n²) lagi. Pake HashSet. |
| Balikin database lookup ke `GetComponent` + linear scan | Pake dictionary. |

---

## Testing Checklist

Setelah ngubah sesuatu, test ini:

- [ ] Cursor lock/unlock di garasi & battlefield
- [ ] Alt free look di dalam mobil
- [ ] Masuk/keluar mobil (turret on/off, cursor lock/unlock)
- [ ] Tembak senjata (pool spawn, casing eject, muzzle flash)
- [ ] Reload senjata (magazine drop, ammo pool)
- [ ] Inventory drag & drop module
- [ ] Save/load grid (file-based)
- [ ] Switch scene (garasi ↔ battlefield)
- [ ] Escape balik ke garasi
