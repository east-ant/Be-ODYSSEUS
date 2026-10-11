using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 화살이 날아가는 모습. 발사 판정(ShotJudge.Resolved)이 나오면 화면 아래(활 쪽)에서 화살을 쏘아
    /// 판정한 자리까지 날려 보내고, 도착하면 Landed를 알린다. 게임 진행(점수·몬스터 맞음)과 이펙트는 Landed에서
    /// 처리해서 화살이 닿는 순간 맞는다. 맞았는지는 쏜 순간 이미 정해져 있고, 날아가는 동안 몬스터가 움직이면 따라간다.
    /// 꽂히면 대상에 붙어 잠깐 부르르 떨다 사라지고, 지나치면 계속 날아가며 사라진다(IShotTarget.ArrowSticks).
    /// 화살 그림은 옆모습이라, 카메라를 향한 채 화면에서 날아가는 방향으로 돌려 그린다(멀어질수록 작아 보인다).
    /// </summary>
    public class ArrowFlight : MonoBehaviour
    {
        // 꽂혔을 때 떨림: 처음 기울기(도), 1초에 떨리는 횟수, 줄어드는 빠르기.
        private const float WobbleDegrees = 7f;
        private const float WobblePerSecond = 9f;
        private const float WobbleDamping = 7f;
        // 사라질 때 투명해지는 시간(초).
        private const float FadeSeconds = 0.25f;

        private enum Stage { Flying, Stuck, Passing }

        private class Arrow
        {
            public SpriteRenderer Renderer;
            public TrailRenderer Trail;
            public ShotResult Result;
            public Stage Stage;
            public bool Sticks;
            public Vector3 Start;
            // 꽂힐 대상(움직이면 따라간다)과 그 대상 기준 도착점. 대상이 없으면 Anchor는 null이고 WorldEnd를 쓴다.
            public Transform Anchor;
            public Vector3 LocalEnd;
            public Vector3 WorldEnd;
            public Vector3 Direction;
            public float Duration;
            public float Time;
        }

        [SerializeField] private ShotJudge _shotJudge;
        [Tooltip("AR 카메라. 화살은 이 앞 아래쪽에서 나온다.")]
        [SerializeField] private Transform _viewer;
        [Tooltip("화살 그림(촉이 위를 향한 옆모습).")]
        [SerializeField] private Sprite _arrowSprite;
        [Tooltip("끄면 화살을 그리지 않고 판정 즉시 맞는다(예전처럼).")]
        [SerializeField] private bool _flyArrows = true;
        [Tooltip("화살이 날아가는 빠르기(m/초). 실제 화살보다 느리게 해서 눈에 보이게 한다.")]
        [SerializeField, Min(1f)] private float _speedMetersPerSecond = 22f;
        [Tooltip("화살 길이(m). 몬스터(키 0.5m)가 멀리 있는 큰 괴물처럼 보이도록 실제 화살보다 작게 한다.")]
        [SerializeField, Min(0.02f)] private float _lengthMeters = 0.18f;
        [Tooltip("화살이 나오는 자리(카메라 기준, m). x 오른쪽, y 위, z 앞.")]
        [SerializeField] private Vector3 _startOffset = new(0.05f, -0.2f, 0.45f);
        [Tooltip("날아가는 동안 위로 살짝 휘는 높이(m).")]
        [SerializeField, Min(0f)] private float _arcMeters = 0.05f;
        [Tooltip("꽂힌 화살이 남아 있는 시간(초).")]
        [SerializeField, Min(0f)] private float _stuckSeconds = 0.8f;
        [Tooltip("빗나가거나 통과한 화살이 도착점을 지나 더 날아가는 거리(m). 이 동안 흐려지며 사라진다.")]
        [SerializeField, Min(0f)] private float _passBeyondMeters = 2f;
        [Tooltip("그리는 순서. 몬스터·도끼보다 위, 명중 이펙트(10)보다 아래.")]
        [SerializeField] private int _sortingOrder = 5;

        [Header("꼬리(잔상)")]
        [Tooltip("날아가는 화살 뒤에 남는 꼬리. 빠르고 작은 화살이 어디로 가는지 보이게 한다.")]
        [SerializeField] private bool _trail = true;
        [Tooltip("꼬리가 남아 있는 시간(초). 길수록 꼬리가 길다.")]
        [SerializeField, Min(0.01f)] private float _trailSeconds = 0.12f;
        [Tooltip("꼬리 굵기(화살 길이 대비).")]
        [SerializeField, Range(0.02f, 1f)] private float _trailWidth01 = 0.25f;
        [SerializeField] private Color _trailColor = new(1f, 0.85f, 0.45f, 0.7f);

        private readonly List<Arrow> _arrows = new();

        /// <summary>화살이 판정한 자리에 닿았을 때(화살을 그리지 않으면 판정 즉시).</summary>
        public event Action<ShotResult> Landed;

        private void OnEnable() => _shotJudge.Resolved += OnShotResolved;
        private void OnDisable() => _shotJudge.Resolved -= OnShotResolved;

        /// <summary>날아가거나 꽂혀 있는 화살을 모두 치운다. 아직 닿지 않은 화살은 Landed를 알리지 않는다.</summary>
        public void Clear()
        {
            foreach (Arrow arrow in _arrows) Release(arrow);
        }

        private void OnShotResolved(ShotResult result)
        {
            if (!_flyArrows)
            {
                Landed?.Invoke(result);
                return;
            }

            Arrow arrow = Next();
            arrow.Result = result;
            arrow.Stage = Stage.Flying;
            arrow.Time = 0f;
            arrow.Start = _viewer.TransformPoint(_startOffset);

            IShotTarget target = _shotJudge.Target;
            arrow.Sticks = target != null && target.IsActive && target.ArrowSticks(result.Hit);
            arrow.Anchor = arrow.Sticks && target is Component component ? component.transform : null;
            if (arrow.Anchor != null) arrow.LocalEnd = arrow.Anchor.InverseTransformPoint(result.ImpactPoint);
            arrow.WorldEnd = result.ImpactPoint;

            Vector3 path = result.ImpactPoint - arrow.Start;
            arrow.Direction = path.sqrMagnitude > 1e-6f ? path.normalized : _viewer.forward;
            arrow.Duration = Mathf.Max(0.05f, path.magnitude / _speedMetersPerSecond);

            arrow.Renderer.color = Color.white;
            arrow.Renderer.gameObject.SetActive(true);
            Place(arrow, arrow.Start, 0f);
            if (arrow.Trail != null)
            {
                arrow.Trail.Clear(); // 지난번 자리에서 이어지지 않게
                arrow.Trail.emitting = true;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // Landed를 받은 쪽에서 새 화살을 쏠 수도 있으니 foreach 대신 번호로 돈다.
            for (int i = 0; i < _arrows.Count; i++)
            {
                Arrow arrow = _arrows[i];
                if (!arrow.Renderer.gameObject.activeSelf) continue;
                arrow.Time += dt;
                switch (arrow.Stage)
                {
                    case Stage.Flying: Fly(arrow); break;
                    case Stage.Stuck: Stick(arrow); break;
                    case Stage.Passing: Pass(arrow); break;
                }
            }
        }

        private void Fly(Arrow arrow)
        {
            float t = Mathf.Clamp01(arrow.Time / arrow.Duration);
            Vector3 end = End(arrow);
            Vector3 tip = Vector3.Lerp(arrow.Start, end, t) + Vector3.up * (_arcMeters * 4f * t * (1f - t));
            Place(arrow, tip, 0f);
            if (t < 1f) return;

            arrow.Stage = arrow.Sticks ? Stage.Stuck : Stage.Passing;
            arrow.Time = 0f;
            // 꽂힌 화살은 몬스터를 따라 움직이므로 꼬리를 더 남기지 않는다.
            if (arrow.Trail != null && arrow.Sticks) arrow.Trail.emitting = false;
            Landed?.Invoke(arrow.Result);
        }

        /// <summary>꽂힌 채 대상을 따라가며 부르르 떨다가 흐려지며 사라진다. 대상이 사라지면 같이 사라진다.</summary>
        private void Stick(Arrow arrow)
        {
            bool anchorGone = arrow.Anchor != null && !arrow.Anchor.gameObject.activeInHierarchy;
            if (anchorGone || arrow.Time >= _stuckSeconds)
            {
                Release(arrow);
                return;
            }
            float wobble = WobbleDegrees * Mathf.Exp(-WobbleDamping * arrow.Time) * Mathf.Sin(arrow.Time * WobblePerSecond * 2f * Mathf.PI);
            Place(arrow, End(arrow), wobble);
            SetAlpha(arrow, Mathf.Clamp01((_stuckSeconds - arrow.Time) / FadeSeconds));
        }

        /// <summary>도착점을 지나 계속 날아가며 흐려진다.</summary>
        private void Pass(Arrow arrow)
        {
            float travelled = arrow.Time * _speedMetersPerSecond;
            if (travelled >= _passBeyondMeters)
            {
                Release(arrow);
                return;
            }
            Place(arrow, arrow.WorldEnd + arrow.Direction * travelled, 0f);
            SetAlpha(arrow, 1f - travelled / _passBeyondMeters);
        }

        private Vector3 End(Arrow arrow) => arrow.Anchor != null ? arrow.Anchor.TransformPoint(arrow.LocalEnd) : arrow.WorldEnd;

        /// <summary>
        /// 촉이 tip에 오게 놓는다. 그림은 카메라를 향하게 하고, 화면에서 날아가는 방향으로 촉이 가게 돌린다.
        /// wobbleDegrees만큼 촉을 축으로 더 기울인다(꽂혔을 때 떨림).
        /// </summary>
        private void Place(Arrow arrow, Vector3 tip, float wobbleDegrees)
        {
            Vector3 view = tip - _viewer.position;
            if (view.sqrMagnitude < 1e-6f) view = _viewer.forward;
            view.Normalize();
            Vector3 screenDirection = Vector3.ProjectOnPlane(arrow.Direction, view);
            if (screenDirection.sqrMagnitude < 1e-8f) screenDirection = _viewer.up;

            Quaternion rotation = Quaternion.LookRotation(view, screenDirection) * Quaternion.Euler(0f, 0f, wobbleDegrees);
            Transform t = arrow.Renderer.transform;
            t.rotation = rotation;
            t.localScale = Vector3.one * (_lengthMeters / _arrowSprite.bounds.size.y);
            // 그림 기준점이 가운데라, 촉(위 끝)이 tip에 오도록 길이의 절반만큼 뒤로 뺀다.
            t.position = tip - rotation * Vector3.up * (_lengthMeters * 0.5f);
        }

        private static void SetAlpha(Arrow arrow, float alpha)
        {
            Color color = arrow.Renderer.color;
            color.a = alpha;
            arrow.Renderer.color = color;
        }

        private static void Release(Arrow arrow) => arrow.Renderer.gameObject.SetActive(false);

        private Arrow Next()
        {
            foreach (Arrow arrow in _arrows)
                if (!arrow.Renderer.gameObject.activeSelf) return arrow;

            var go = new GameObject("Arrow", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = _arrowSprite;
            renderer.sortingOrder = _sortingOrder;
            var created = new Arrow { Renderer = renderer, Trail = _trail ? CreateTrail(go, renderer) : null };
            _arrows.Add(created);
            return created;
        }

        /// <summary>화살 뒤로 가늘어지며 사라지는 꼬리. 재질은 화살 그림과 같은 스프라이트 재질을 쓴다(색만 입힌다).</summary>
        private TrailRenderer CreateTrail(GameObject arrow, SpriteRenderer renderer)
        {
            var trail = arrow.AddComponent<TrailRenderer>();
            trail.sharedMaterial = renderer.sharedMaterial;
            trail.sortingOrder = _sortingOrder - 1;
            trail.time = _trailSeconds;
            trail.minVertexDistance = 0.01f;
            trail.widthMultiplier = _lengthMeters * _trailWidth01;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            // 화살 쪽은 진하고 끝으로 갈수록 투명해진다.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(_trailColor, 0f), new GradientColorKey(_trailColor, 1f) },
                new[] { new GradientAlphaKey(_trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }
    }
}
