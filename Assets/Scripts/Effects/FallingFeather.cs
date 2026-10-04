using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 몬스터가 날아갈 때 몸에서 떨어지는 깃털 한 장. AR 공간 그 자리에 남아,
    /// 처음엔 몬스터가 가던 쪽으로 조금 밀려가다가 좌우로 흔들리며 천천히 떨어지고, 빙글 돌면서 서서히 투명해진 뒤 꺼진다.
    /// 항상 카메라를 바라본다. 거리·속도는 몬스터 키(unitMeters) 대비로 정한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FallingFeather : MonoBehaviour
    {
        // 깃털이라 천천히 떨어진다: 떨어지는 속도는 이보다 빨라지지 않는다(몬스터 키 대비, 초당).
        private const float MaxFallSpeed = 0.45f;
        // 처음 받은 속도가 떨어지는 속도로 바뀌는 빠르기(몬스터 키 대비, 초당 속도 변화).
        private const float Drag = 1.6f;
        // 좌우로 흔들리는 횟수(초당)와 폭(몬스터 키 대비).
        private const float SwayPerSecond = 1.4f;
        private const float SwayAmount = 0.05f;
        // 빙글 도는 속도 범위(도/초). 방향은 무작위.
        private const float MinSpinDegrees = 90f;
        private const float MaxSpinDegrees = 300f;
        // 수명 중 이 지점부터 투명해지기 시작한다.
        private const float FadeFrom = 0.45f;

        private SpriteRenderer _renderer;
        private Transform _viewer;
        private Vector3 _basePosition;
        private Vector3 _velocity;
        private float _unitMeters;
        private float _life;
        private float _elapsed;
        private float _swayPhase;
        private float _spinSpeed;
        private float _spin;

        public bool IsPlaying => gameObject.activeSelf;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>깃털을 position에 놓고 velocity(m/s)로 떨어지기 시작한다. scale은 몬스터 그림과 같은 배율.</summary>
        public void Play(Sprite sprite, Vector3 position, Vector3 velocity, Transform viewer, float scale,
            float unitMeters, float lifeSeconds, int sortingOrder)
        {
            _renderer.sprite = sprite;
            _renderer.sortingOrder = sortingOrder;
            _renderer.color = Color.white;
            _viewer = viewer;
            _basePosition = position;
            _velocity = velocity;
            _unitMeters = unitMeters;
            _life = Mathf.Max(lifeSeconds, 0.05f);
            _elapsed = 0f;
            _swayPhase = Random.Range(0f, 2f * Mathf.PI);
            _spinSpeed = Random.Range(MinSpinDegrees, MaxSpinDegrees) * (Random.value < 0.5f ? -1f : 1f);
            _spin = Random.Range(0f, 360f);
            transform.localScale = Vector3.one * scale;
            gameObject.SetActive(true);
            Apply();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _elapsed += dt;
            if (_elapsed >= _life)
            {
                gameObject.SetActive(false);
                return;
            }

            _velocity = Vector3.MoveTowards(_velocity, Vector3.down * (MaxFallSpeed * _unitMeters), Drag * _unitMeters * dt);
            _basePosition += _velocity * dt;
            _spin += _spinSpeed * dt;
            Apply();
        }

        private void Apply()
        {
            Vector3 away = _basePosition - _viewer.position;
            away.y = 0f;
            Quaternion facing = away.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(away, Vector3.up) : transform.rotation;

            // 카메라에서 볼 때 좌우로 흔들린다.
            float sway = Mathf.Sin(_swayPhase + _elapsed * 2f * Mathf.PI * SwayPerSecond) * SwayAmount * _unitMeters;
            transform.position = _basePosition + facing * Vector3.right * sway;
            transform.rotation = facing * Quaternion.Euler(0f, 0f, _spin);

            float t = _elapsed / _life;
            Color color = _renderer.color;
            color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeFrom, 1f, t));
            _renderer.color = color;
        }
    }
}
