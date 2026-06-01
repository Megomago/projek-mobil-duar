using UnityEngine;

namespace Weapons
{
    [RequireComponent(typeof(Animator))]
    public class ReloadAnim : MonoBehaviour
    {
        [Header("Weapon Reference")]
        [Tooltip("Pilih objek ModularWeapon yang memicu animasi ini")]
        public ModularWeapon targetWeapon;

        [Header("Animator Settings")]
        [Tooltip("Biarkan kosong jika script ini ada di GameObject yang sama dengan Animator")]
        public Animator weaponAnimator;
        
        [Tooltip("Nama trigger parameter di Animator untuk memulai reload")]
        public string reloadTriggerName = "Reload";

        private void Awake()
        {
            if (weaponAnimator == null)
            {
                weaponAnimator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            if (targetWeapon != null)
            {
                targetWeapon.OnReloadStart += TriggerReloadAnimation;
            }
            else
            {
                Debug.LogWarning("ReloadAnim: Target Weapon belum di-assign! Pastikan ModularWeapon sudah di-drag ke inspector.", this);
            }
        }

        private void OnDisable()
        {
            if (targetWeapon != null)
            {
                targetWeapon.OnReloadStart -= TriggerReloadAnimation;
            }
        }

        private void TriggerReloadAnimation()
        {
            if (weaponAnimator != null && !string.IsNullOrEmpty(reloadTriggerName))
            {
                weaponAnimator.SetTrigger(reloadTriggerName);
            }
        }
    }
}
