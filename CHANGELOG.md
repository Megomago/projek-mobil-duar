# Changelog

## [Unreleased]

### Add
- HitboxProxy + VehicleHitboxInitializer — cache komponen collision biar gak traverse hierarchy tiap tembak
- ExplosionManager — sistem ledakan terpusat buat module amunisi HE
- WheelHealth — roda sekarang punya HP sendiri, bisa pecah
- SimpleTarget — target non-kendaraan (generator, panel, dll) bisa kena damage
- PlayerController + VehicleEntry — player bisa jalan kaki, naik/turun mobil
- VehicleGridSystem — sistem grid modular (zona atap/kap/dll), simpan/load, preset default
- GridVisualizer — visual grid overlay biar keliatan cellnya
- GridSaveSystem — nyimpen konfigurasi module di kendaraan
- OptFormula — formula penetrasi + armor scaling + exit velocity
- ModuleDatabase — registry semua module yang ada
- ModuleSelectionManager + UIModuleItem — UI milih module dari inventory
- VehicleModuleComponent — component module yang nerima damage
- InventoryDragDropManager — drag & drop module di grid
- LoadoutManager — UI loadout module
- VehicleModuleListUI — daftar module terpasang
- BattlefieldManager — manajemen scene battlefield
- CircularBuffer — utility buat perf tracking
- Amunisi explosive — module bisa meledak kena hit
- Air drag — peluru kena hambatan udara
- Module Aki 540 Wh — baterai besar baru
- Senjata Unprecision 4/15 — shotgun gauge 4
- Senjata Big Buster 50 — module variant
- L21 RARDEN — modular version
- Grandmax Van MBG — kendaraan baru lengkap dengan data, decal, texture
- Mobil jalan kaki — player bisa turun dan jalan
- Debug visualizer (F2/F3) — hit indicator + aim line visible di build
- Garasi 2 scene — atlas texture, material baru, manusia prefab
- Desert terrain + skybox + pasir material + partikel debu
- Icon UI baru — battery, engine, oil, tire, weight, shield, grid, dll
- SFX senjata — 20mm, rarden, precision, starter, v8 engine
- documentation.md — dokumentasi teknis proyek

### Change
- VehicleStatsManager di-refactor — pisah grid logic, pake null-safe empty list
- VehicleController — modular integration, grid-based stats
- ModularWeapon — modular weapon system (swap module, clearance)
- ManualTurretController — pake RaycastNonAlloc, tambah debug visualizer
- WeaponData — tambah field explosive, ammoCostPerShot, pitch/volume
- KinematicProjectile — pake HitboxProxy, zero hierarchy traversal
- ObjectPool — generic Spawn<T>
- ModuleTemplate — tambah grid dimension, clearance, moduleType
- OptFormula — remainingPen scaling pake velRatio (bukan subtract def)
- balanctool/index.html — tambah PEN field, armor DEF simulation
- VehicleHUD dipindah ke Scripts/UI/
- SceneController, Lobbycamera dipindah ke Scripts/Scene/
- Prefab Grandmax dipindah ke Van MBG folder
- Sedan AK, Sedan prefab update — integration sama system baru
- Battlefield scene — update lighting, posisi kendaraan, health value
- Rarden data — penetration 800→500
- Oerlikon data — ammo 60→180, tambah explosive field
- Unprecision 4/15 data — tambah ATK, PEN, ammoCost, pitch/volume
- EngineAudio — vehicle engine sounds
- WheelEffects — continuous dust trail + slip smoke
- Tuning Grandmax — engine params, gear ratios, shift RPM

### Fix
- Double-count velocity ratio di EffectivePen + remainingPen
- NRE standalone module — null check placedModuleData.moduleTemplate
- Module damage tanpa VehicleStatsManager
- TriggerChainExplosion Debug.Log dibungkus #if UNITY_EDITOR
- currentBatteryAmount/currentFuelAmount sekarang nambah sesuai delta kapasitas pasang modul baru
- Recoil pas velocity 0
- Sync current ammo
- Rotasi module senjata samping
- Warning unused variable `loaded` di VehicleGridSystem
- Trigger collider sekarang gak di-skip oleh VehicleHitboxInitializer

### Remove
- Camera shake dari explosion (module + projectile HE)
- Script lama di Assets/Weapons/, Assets/Wheel/ — dipindah ke Assets/Scripts/
- GridCell sprite dari Battlefield scene (pindah ke GridVisualizer runtime)
- Oerlikon anim controller lama
- Lightmap lama (Lightmap-0_comp_dir.png, Lightmap-0_comp_light.exr)
