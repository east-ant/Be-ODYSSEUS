using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 발사 판정이 나올 때마다 화살이 닿은 자리에 이펙트를 띄운다.
    /// 명중이면 별빛(starburst), 빗나가면 충격(impact) 이펙트.
    /// </summary>
    public class ShotEffects : MonoBehaviour
    {
        // 몬스터·범위 테두리보다 위에 그려지게 하는 정렬 순서.
        private const int SortingOrder = 10;
        // 같은 깊이의 몬스터 이미지와 겹쳐 깜빡이지 않게 카메라 쪽으로 살짝 당기는 거리(m).
        private const float PullTowardViewerMeters = 0.05f;

        [SerializeField] private ShotJudge _shotJudge;
        [Tooltip("AR 카메라. 이펙트가 이쪽을 바라본다.")]
        [SerializeField] private Transform _viewer;
        [SerializeField] private Sprite _hitSprite;
        [SerializeField] private Sprite _missSprite;
        [Tooltip("명중 이펙트 크기(m).")]
        [SerializeField, Min(0.01f)] private float _hitSizeMeters = 0.7f;
        [Tooltip("빗나감 이펙트 크기(m).")]
        [SerializeField, Min(0.01f)] private float _missSizeMeters = 0.6f;
        [Tooltip("이펙트가 떠 있는 시간(초).")]
        [SerializeField, Min(0.05f)] private float _durationSeconds = 0.6f;

        private readonly List<FxPop> _pool = new();

        private void OnEnable() => _shotJudge.Resolved += OnShotResolved;
        private void OnDisable() => _shotJudge.Resolved -= OnShotResolved;

        private void OnShotResolved(ShotResult result)
        {
            Vector3 toViewer = (_viewer.position - result.ImpactPoint).normalized;
            Vector3 position = result.ImpactPoint + toViewer * PullTowardViewerMeters;
            if (result.Hit) Next().Play(_hitSprite, position, _viewer, _hitSizeMeters, _durationSeconds);
            else Next().Play(_missSprite, position, _viewer, _missSizeMeters, _durationSeconds);
        }

        private FxPop Next()
        {
            foreach (FxPop fx in _pool)
                if (!fx.IsPlaying) return fx;

            var go = new GameObject("ShotFx", typeof(SpriteRenderer), typeof(FxPop));
            go.transform.SetParent(transform, false);
            go.GetComponent<SpriteRenderer>().sortingOrder = SortingOrder;
            var created = go.GetComponent<FxPop>();
            _pool.Add(created);
            return created;
        }
    }
}
