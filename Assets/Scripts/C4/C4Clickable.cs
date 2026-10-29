using System.Collections;
using UnityEngine;

namespace C4
{
    public enum ClickableType
    {
        Knob,
        KeypadNumber,
        KeypadStar,
        KeypadClear
    }

    [RequireComponent(typeof(Collider))]
    public class C4Clickable : MonoBehaviour
    {
        public ClickableType type;
        public int numberValue;

        [Header("=== BUTTON PRESS ANIMATION ===")]
        [Tooltip("Arah mendemnya tombol. Coba (0, 0, -0.003) atau (0, -0.003, 0) tergantung sumbu model lu.")]
        public Vector3 pressOffset = new Vector3(0f, 0f, -0.003f);
        public float pressDuration = 0.08f;

        [Header("=== HIGHLIGHT / OUTLINE ===")]
        public Renderer buttonRenderer;
        public Color hoverEmissionColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Warna pendar pas disorot

        private Vector3 _originalLocalPos;
        private Coroutine _pressCoroutine;
        private Material _matInstance;
        private Color _originalEmission;
        private bool _hasEmission;

        private void Awake()
        {
            _originalLocalPos = transform.localPosition;

            if (buttonRenderer == null)
                buttonRenderer = GetComponent<Renderer>();

            if (buttonRenderer != null)
            {
                _matInstance = buttonRenderer.material; // Instance unik per tombol
                if (_matInstance.HasProperty("_EmissionColor"))
                {
                    _hasEmission = true;
                    _originalEmission = _matInstance.GetColor("_EmissionColor");
                }
            }
        }

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // Dipanggil pas disorot kursor
        public void SetHover(bool isHovered)
        {
            if (!_hasEmission || _matInstance == null) return;

            if (isHovered)
            {
                _matInstance.EnableKeyword("_EMISSION");
                _matInstance.SetColor("_EmissionColor", hoverEmissionColor);
            }
            else
            {
                _matInstance.SetColor("_EmissionColor", _originalEmission);
                if (_originalEmission == Color.black)
                    _matInstance.DisableKeyword("_EMISSION");
            }
        }

        // Dipanggil pas diklik
        public void AnimatePress()
        {
            // Knob ga usah mendem, dia kan muter
            if (type == ClickableType.Knob) return;

            if (_pressCoroutine != null)
                StopCoroutine(_pressCoroutine);

            _pressCoroutine = StartCoroutine(PressRoutine());
        }

        private IEnumerator PressRoutine()
        {
            Vector3 targetPos = _originalLocalPos + pressOffset;
            float halfDuration = pressDuration * 0.5f;

            // 1. Tombol mendem ke dalem
            float t = 0f;
            while (t < halfDuration)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(_originalLocalPos, targetPos, t / halfDuration);
                yield return null;
            }

            // 2. Tombol mantul balik ke posisi awal
            t = 0f;
            while (t < halfDuration)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(targetPos, _originalLocalPos, t / halfDuration);
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            _pressCoroutine = null;
        }
    }
}