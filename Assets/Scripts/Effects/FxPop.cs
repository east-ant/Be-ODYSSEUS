using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 한 번 터지고 사라지는 2D 이펙트. AR 공간의 한 점에 떠서 항상 카메라를 바라보고,
    /// 잠깐 커지면서 서서히 투명해진 뒤 꺼진다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FxPop : MonoBehaviour
    {
        // 처음 크기와 끝 크기(설정 크기 대비 배율).
        private const float StartScale = 0.6f;
        private const float EndScale = 1.3f;

        private SpriteRenderer _renderer;
        private Transform _viewer;
        private float _baseScale;
        private float _duration;
        private float _elapsed;

        public bool IsPlaying => gameObject.activeSelf;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(Sprite sprite, Vector3 position, Transform viewer, float sizeMeters, float durationSeconds)
        {
            _renderer.sprite = sprite;
            _viewer = viewer;
            _baseScale = sizeMeters / sprite.bounds.size.y;
            _duration = Mathf.Max(durationSeconds, 0.01f);
            _elapsed = 0f;
            transform.position = position;
            gameObject.SetActive(true);
            Apply(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            Apply(t);
        }

        // t: 0 → 1. 크기는 처음에 빨리 커지고(ease-out), 투명도는 뒤로 갈수록 빨리 빠진다.
        private void Apply(float t)
        {
            float grow = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * (_baseScale * Mathf.Lerp(StartScale, EndScale, grow));

            Color color = _renderer.color;
            color.a = 1f - t * t;
            _renderer.color = color;

            Vector3 away = transform.position - _viewer.position;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
