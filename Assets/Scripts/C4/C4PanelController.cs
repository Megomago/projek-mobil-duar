using TMPro;
using UnityEngine;

namespace C4
{
    public enum KnobState
    {
        Off,
        Prepare,
        Standby
    }

    public class C4PanelController : MonoBehaviour
    {
        [Header("=== KNOB SETTINGS ===")]
        public KnobState currentKnobState = KnobState.Off;
        public Transform knobTransform;
        public Vector3 knobOffEuler = new Vector3(0, 0, 0);
        public Vector3 knobPrepareEuler = new Vector3(0, 45, 0);
        public Vector3 knobStandbyEuler = new Vector3(0, 90, 0);

        [Tooltip("Lama waktu putaran knob (detik). 0.15 detik pas bgt buat feel klik mekanik.")]
        public float knobRotateDuration = 0.15f;

        private Coroutine _knobCoroutine;

        [Header("=== LED & EMISSION ===")]
        public Renderer ledRenderer;

        [Tooltip("Biar tekstur PNG emission-nya nyala terang/berpendar.")]
        public float emissionIntensity = 2.0f;

        [Header("=== DISPLAY SCREEN ===")]
        public TMP_Text screenText; // TextMeshPro UI / World Space
        public int maxCodeLength = 5;
        private string _enteredCode = "";

        [Header("=== AUDIO (OPSIONAL BIAR KEK DCS) ===")]
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip knobSound;

        private Material _ledMaterial;

        private void Awake()
        {
            if (ledRenderer != null)
                _ledMaterial = ledRenderer.material; // Unique instance

            UpdateKnobVisual();
            UpdateScreenVisual();
        }

        // Method ini yang dipanggil pas player ngeklik sesuatu di C4
        public void Interact(C4Clickable target)
        {
            if (target == null) return;

            switch (target.type)
            {
                case ClickableType.Knob:
                    CycleKnob();
                    break;

                case ClickableType.KeypadNumber:
                    PressNumber(target.numberValue);
                    break;

                case ClickableType.KeypadClear:
                    PressClear();
                    break;

                case ClickableType.KeypadStar:
                    PressStar();
                    break;
            }
        }

        private void CycleKnob()
        {
            PlaySound(knobSound != null ? knobSound : clickSound);

            // Ganti mode memutar: Off -> Prepare -> Standby -> Off
            if (currentKnobState == KnobState.Off) currentKnobState = KnobState.Prepare;
            else if (currentKnobState == KnobState.Prepare) currentKnobState = KnobState.Standby;
            else currentKnobState = KnobState.Off;

            // Kalau mati atau prepare, reset kode inputan
            if (currentKnobState != KnobState.Standby)
            {
                _enteredCode = "";
            }

            UpdateKnobVisual();
            UpdateScreenVisual();
        }

        private void PressNumber(int num)
        {
            // Keypad cuma aktif pas mode STANDBY
            if (currentKnobState != KnobState.Standby) return;

            PlaySound(clickSound);

            if (_enteredCode.Length < maxCodeLength)
            {
                _enteredCode += num.ToString();
                UpdateScreenVisual();
            }
        }

        private void PressClear()
        {
            if (currentKnobState != KnobState.Standby) return;

            PlaySound(clickSound);

            if (_enteredCode.Length > 0)
            {
                // Backspace (hapus 1 digit terakhir)
                _enteredCode = _enteredCode.Substring(0, _enteredCode.Length - 1);
                UpdateScreenVisual();
            }
        }

        private void PressStar()
        {
            if (currentKnobState != KnobState.Standby) return;

            PlaySound(clickSound);

            Debug.Log($"[C4] Kode dikonfirmasi: {_enteredCode}");
            // Nanti lu bisa tambahin trigger ledakan / arming di sini
        }

        private void UpdateKnobVisual()
        {
            // 1. Animasi Muter Halus (Slerp)
            if (knobTransform != null)
            {
                Vector3 targetEuler = knobOffEuler;
                if (currentKnobState == KnobState.Prepare) targetEuler = knobPrepareEuler;
                else if (currentKnobState == KnobState.Standby) targetEuler = knobStandbyEuler;

                // Stop animasi sebelumnya kalau player ngeklik cepet-cepet
                if (_knobCoroutine != null)
                    StopCoroutine(_knobCoroutine);

                _knobCoroutine = StartCoroutine(AnimateKnobRoutine(Quaternion.Euler(targetEuler)));
            }

            // 2. Saklar Tekstur PNG Emission
            if (_ledMaterial != null)
            {
                if (currentKnobState == KnobState.Off)
                {
                    _ledMaterial.DisableKeyword("_EMISSION");
                    _ledMaterial.SetColor("_EmissionColor", Color.black);
                }
                else
                {
                    _ledMaterial.EnableKeyword("_EMISSION");
                    _ledMaterial.SetColor("_EmissionColor", Color.white * emissionIntensity);
                }
            }
        }

        // Coroutine rotasi mekanik
        private System.Collections.IEnumerator AnimateKnobRoutine(Quaternion targetRot)
        {
            Quaternion startRot = knobTransform.localRotation;
            float elapsed = 0f;

            while (elapsed < knobRotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / knobRotateDuration;

                // SmoothStep: ngasih efek akselerasi-deselerasi per mekanik (ga kaku)
                t = Mathf.SmoothStep(0f, 1f, t);

                // Quaternion.Slerp OTOMATIS muter balik lewat rute terpendek!
                knobTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            knobTransform.localRotation = targetRot;
            _knobCoroutine = null;
        }

        private void UpdateScreenVisual()
        {
            if (screenText == null) return;

            if (currentKnobState == KnobState.Off)
            {
                screenText.text = ""; // Layar mati
            }
            else if (currentKnobState == KnobState.Prepare)
            {
                screenText.text = "*-*"; // Atau teks boot-up animasi
            }
            else if (currentKnobState == KnobState.Standby)
            {
                // Format: teks inputan lu + sisa garis bawah _____
                string display = _enteredCode;
                int remaining = maxCodeLength - _enteredCode.Length;
                for (int i = 0; i < remaining; i++)
                {
                    display += "_";
                }
                screenText.text = display;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
    }
}