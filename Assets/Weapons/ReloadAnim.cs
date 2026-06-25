using System.Collections;
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

        [Tooltip("Delay (dalam detik) sebelum trigger animasi dijalankan")]
        public float animationDelay = 0f;

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
                if (animationDelay > 0f && gameObject.activeInHierarchy)
                {
                    StartCoroutine(DelayedTrigger());
                }
                else
                {
                    weaponAnimator.SetTrigger(reloadTriggerName);
                }
            }
        }

        private IEnumerator DelayedTrigger()
        {
            yield return new WaitForSeconds(animationDelay);
            if (weaponAnimator != null)
            {
                weaponAnimator.SetTrigger(reloadTriggerName);
            }
        }
    }
}
