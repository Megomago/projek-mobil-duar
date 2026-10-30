using System.Collections;
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

    [RequireComponent(typeof(C4Explosive))]
    public class C4PanelController : MonoBehaviour
    {
        [Header("=== KNOB SETTINGS ===")]
        public KnobState currentKnobState = KnobState.Off;
        public Transform knobTransform;
        public Vector3 knobOffEuler = new Vector3(0, 0, 0);       // Kiri (0)
        public Vector3 knobPrepareEuler = new Vector3(0, 90, 0);   // Tengah (90)
        public Vector3 knobStandbyEuler = new Vector3(0, 180, 0);  // Kanan (180)
        public float knobRotateDuration = 0.15f;

        [Header("=== LED & EMISSION ===")]
        public Renderer ledRenderer;
        public int ledMaterialIndex = 0;
        public float emissionIntensity = 2.0f;
        public Color ledArmedColor = Color.red; // Warna kedip pas timer jalan

        [Header("=== DISPLAY & STICKY NOTE ===")]
        public TMP_Text screenText;     // Layar LCD C4
        public TMP_Text stickyNoteText; // Teks di kertas Post-it Note
        public int codeLength = 5;

        [Header("=== COUNTDOWN & DETONATION ===")]
        public float countdownDuration = 15f; // Durasi timer
        public float maxBeepInterval = 1.0f;  // Interval bunyi awal (lambat)
        public float minBeepInterval = 0.08f; // Interval bunyi akhir (ngebut bgt)

        [Header("=== AUDIO (SFX) ===")]
        public AudioSource audioSource;
        public AudioClip clickSound;
        public AudioClip knobSound;
        public AudioClip beepSound;   // Suara tit... tit...
        public AudioClip errorSound;  // Suara salah pin
        public AudioClip armSound;    // Suara pas pin bener / timer mulai

        private Material _ledMaterial;
        private Coroutine _knobCoroutine;
        private Coroutine _countdownCoroutine;
        private Vector3 _currentKnobEuler;

        private string _generatedCode = "";
        private string _enteredCode = "";
        private bool _isArmed = false;
        private bool _isErrorShowing = false;

        private C4Explosive _explosive;

        public string GeneratedCode => _generatedCode;

        private void Awake()
        {
            _explosive = GetComponent<C4Explosive>();
            if (_explosive == null)
                _explosive = gameObject.AddComponent<C4Explosive>();

            // Pastikan C4Outline selalu terpasang untuk visual outline & raycast detection
            if (GetComponent<C4Outline>() == null)
                gameObject.AddComponent<C4Outline>();

            // Setup AudioSource untuk suara tombol & beep 3D (tidak terdengar di seluruh map)
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource != null)
            {
                audioSource.spatialBlend = 1.0f; // 3D Audio
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1.5f; // Di bawah 1.5 meter volume 100%
                audioSource.maxDistance = 20.0f; // Di atas 20 meter beep sudah tidak terdengar
                audioSource.dopplerLevel = 0f;
            }

            // Setup Rotasi Knob Awal
            _currentKnobEuler = knobOffEuler;
            if (knobTransform != null)
                knobTransform.localEulerAngles = _currentKnobEuler;

            // Setup Material LED
            if (ledRenderer != null)
            {
                Material[] mats = ledRenderer.materials;
                if (ledMaterialIndex >= 0 && ledMaterialIndex < mats.Length)
                    _ledMaterial = mats[ledMaterialIndex];
                else
                    _ledMaterial = ledRenderer.material;
            }

            // Generate PIN 5 digit acak (10000 - 99999) jika belum ada yang diset
            if (string.IsNullOrEmpty(_generatedCode))
            {
                GenerateNewCode();
            }
            else
            {
                UpdateStickyNoteText();
            }

            UpdateKnobVisual();
            UpdateScreenVisual();
        }

        public void SetCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            _generatedCode = code;
            UpdateStickyNoteText();
        }

        public void GenerateNewCode()
        {
            // Roll angka 5 digit
            int randomNum = Random.Range(10000, 100000);
            SetCode(randomNum.ToString());
        }

        private void UpdateStickyNoteText()
        {
            if (stickyNoteText == null)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i].gameObject.name.ToLower().Contains("note"))
                    {
                        stickyNoteText = texts[i];
                        break;
                    }
                }
            }

            // Tempel ke kertas Post-it: angka + simbol '#'
            if (stickyNoteText != null && !string.IsNullOrEmpty(_generatedCode))
            {
                stickyNoteText.text = _generatedCode + "#";
            }
        }

        public void Interact(C4Clickable target)
        {
            if (target == null) return;

            // KALAU UDAH ARMED / COUNTDOWN, KUNCI SEMUA INTERAKSI!
            if (_isArmed) return;

            // Jangan terima input kalau layar lagi nampilin pesan ERROR
            if (_isErrorShowing) return;

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

                case ClickableType.KeypadStar: // Tombol '#' (Submit)
                    PressSubmit();
                    break;
            }
        }

        private void CycleKnob()
        {
            PlaySound(knobSound != null ? knobSound : clickSound);

            if (currentKnobState == KnobState.Off) currentKnobState = KnobState.Prepare;
            else if (currentKnobState == KnobState.Prepare) currentKnobState = KnobState.Standby;
            else currentKnobState = KnobState.Off;

            if (currentKnobState != KnobState.Standby)
            {
                _enteredCode = "";
            }

            UpdateKnobVisual();
            UpdateScreenVisual();
        }

        private void PressNumber(int num)
        {
            if (currentKnobState != KnobState.Standby) return;

            PlaySound(clickSound);

            if (_enteredCode.Length < codeLength)
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
                _enteredCode = _enteredCode.Substring(0, _enteredCode.Length - 1);
                UpdateScreenVisual();
            }
        }

        private void PressSubmit()
        {
            if (currentKnobState != KnobState.Standby) return;

            // Cek apakah kode cocok sama yang di Post-it Note
            if (_enteredCode == _generatedCode)
            {
                // PIN BENAR -> AKTIFKAN HITUNG MUNDUR!
                PlaySound(armSound != null ? armSound : clickSound);
                StartCountdown();
            }
            else
            {
                // PIN SALAH -> ERROR & CLEAR
                PlaySound(errorSound);
                StartCoroutine(ShowErrorRoutine());
            }
        }

        private void StartCountdown()
        {
            _isArmed = true;

            if (_countdownCoroutine != null)
                StopCoroutine(_countdownCoroutine);

            _countdownCoroutine = StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            float timeLeft = countdownDuration;
            float nextBeepTime = Time.time;

            while (timeLeft > 0f)
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft < 0f) timeLeft = 0f;

                // Format Teks MM:SS (00:15 -> 00:00)
                int seconds = Mathf.FloorToInt(timeLeft % 60);
                int fraction = Mathf.FloorToInt((timeLeft * 100) % 100);
                // Biar dramatis: pas di bawah 1 detik, tampilin milidetik
                if (timeLeft < 1f)
                    screenText.text = string.Format("00:00:{0:00}", fraction);
                else
                    screenText.text = string.Format("00:{0:00}", seconds);

                // Logika Bunyi Beep & Kedap-kedip LED (makin tipis waktu, makin cepet)
                if (Time.time >= nextBeepTime)
                {
                    float progress = 1f - (timeLeft / countdownDuration); // 0 (awal) -> 1 (mau meledak)
                    // Interpolasi eksponensial biar deg-degan
                    float currentInterval = Mathf.Lerp(maxBeepInterval, minBeepInterval, progress * progress);
                    nextBeepTime = Time.time + currentInterval;

                    PlaySound(beepSound);
                    StartCoroutine(FlashLedRoutine(Mathf.Clamp(currentInterval * 0.4f, 0.03f, 0.2f)));
                }

                yield return null;
            }

            // WAKTU HABIS -> DUARRR!
            screenText.text = "00:00";
            if (_explosive != null)
            {
                _explosive.Detonate();
            }
            else
            {
                Debug.LogError("[C4] Komponen C4Explosive ga ketemu! Bom ga bisa meledak.");
            }
        }

        private IEnumerator FlashLedRoutine(float duration)
        {
            if (_ledMaterial == null) yield break;

            // Nyalain LED Merah
            _ledMaterial.EnableKeyword("_EMISSION");
            _ledMaterial.SetColor("_EmissionColor", ledArmedColor * emissionIntensity * 2f);

            yield return new WaitForSeconds(duration);

            // Matiin lagi
            _ledMaterial.SetColor("_EmissionColor", Color.black);
            _ledMaterial.DisableKeyword("_EMISSION");
        }

        private IEnumerator ShowErrorRoutine()
        {
            _isErrorShowing = true;
            _enteredCode = "";

            if (screenText != null)
                screenText.text = "ERROR";

            yield return new WaitForSeconds(0.8f);

            _isErrorShowing = false;
            UpdateScreenVisual(); // Balik ke tampilan '_____'
        }

        private void UpdateKnobVisual()
        {
            Vector3 targetEuler = knobOffEuler;
            if (currentKnobState == KnobState.Prepare) targetEuler = knobPrepareEuler;
            else if (currentKnobState == KnobState.Standby) targetEuler = knobStandbyEuler;

            if (knobTransform != null)
            {
                if (_knobCoroutine != null)
                    StopCoroutine(_knobCoroutine);

                _knobCoroutine = StartCoroutine(AnimateKnobEulerRoutine(_currentKnobEuler, targetEuler));
            }

            if (_ledMaterial != null && !_isArmed)
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

        private IEnumerator AnimateKnobEulerRoutine(Vector3 startEuler, Vector3 targetEuler)
        {
            float elapsed = 0f;
            while (elapsed < knobRotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / knobRotateDuration);
                _currentKnobEuler = Vector3.Lerp(startEuler, targetEuler, t);
                knobTransform.localEulerAngles = _currentKnobEuler;
                yield return null;
            }

            _currentKnobEuler = targetEuler;
            knobTransform.localEulerAngles = targetEuler;
            _knobCoroutine = null;
        }

        private void UpdateScreenVisual()
        {
            if (screenText == null || _isArmed || _isErrorShowing) return;

            if (currentKnobState == KnobState.Off)
            {
                screenText.text = "";
            }
            else if (currentKnobState == KnobState.Prepare)
            {
                screenText.text = "HELLO";
            }
            else if (currentKnobState == KnobState.Standby)
            {
                string display = _enteredCode;
                int remaining = codeLength - _enteredCode.Length;
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
