using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 도끼 튜토리얼(오디세우스의 도끼 시험): 방향을 정할 때 겨눈 방향으로 엇갈린 도끼 한 쌍(axe.png 그대로)을
    /// 여러 개 앞뒤로 세운다. 두 도끼 사이 구멍(머리 아래, 손잡이가 엇갈리는 곳 위)이 모두 한 줄로 겹치게 놓아서
    /// 구멍이 터널처럼 보인다. 화살(조준선)이 모든 쌍의 구멍을 지나야 통과다.
    /// 판정은 그림 픽셀 그대로 한다: 구멍 가운데에서 투명한 픽셀만 따라 채운 영역이 구멍이라,
    /// 화면에서 도끼에 안 닿고 구멍으로 보이는 곳은 모두 통과다(그림 텍스처는 Read/Write가 켜져 있어야 한다).
    /// 방향을 정한 자리에서 벗어나면 구멍이 어긋나 보인다(실제로 줄을 맞춰 서야 하는 것처럼).
    /// </summary>
    public class AxeRow : MonoBehaviour, IShotTarget
    {
        // 이 값보다 불투명하면 도끼(막힘)로 본다.
        private const byte OpaqueAlpha = 128;

        [SerializeField] private GameConfig _config;
        [Tooltip("엇갈린 도끼 한 쌍 그림. 텍스처의 Read/Write가 켜져 있어야 픽셀로 판정한다.")]
        [SerializeField] private Sprite _axeSprite;
        [Tooltip("그림에서 두 도끼 사이 구멍 안의 한 점(왼쪽 아래 0,0 ~ 오른쪽 위 1,1). 줄을 맞추는 기준이자 구멍을 채우기 시작하는 자리.")]
        [SerializeField] private Vector2 _gap01 = new(0.5f, 0.5189f);
        [Tooltip("픽셀 판정을 못 할 때(텍스처를 읽을 수 없을 때)만 쓰는 원 판정 반지름(그림 높이 대비).")]
        [SerializeField, Range(0.01f, 0.3f)] private float _gapRadius01 = 0.0817f;

        private readonly List<SpriteRenderer> _axes = new();
        private int _count;
        private Vector3 _farGap;
        // 구멍 픽셀(스프라이트 사각형 기준, 아래쪽 줄부터). 만들지 못했으면 null이고 원으로 판정한다.
        private bool[] _gapMask;
        private bool _gapMaskBuilt;

        public bool IsActive => this != null && gameObject.activeInHierarchy && _count > 0;

        /// <summary>가장 먼 쌍의 구멍 기준점. 정확도는 이 점을 기준으로 잰다.</summary>
        public Vector3 TargetPoint => _farGap;

        /// <summary>front(방향 정할 때의 카메라 위치·겨눈 방향) 앞으로 도끼 쌍을 한 줄로 세운다.</summary>
        public void Show(Pose front)
        {
            if (!_gapMaskBuilt) BuildGapMask();
            gameObject.SetActive(true);
            Vector3 forward = front.rotation * Vector3.forward;
            // 도끼는 그림 그대로 세워 정면을 보게 하고, 구멍만 겨눈 방향(위아래 포함) 줄에 맞춘다.
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            Quaternion facing = Quaternion.LookRotation(flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward, Vector3.up);
            float scale = _config.AxeHeightMeters / _axeSprite.bounds.size.y;

            _count = _config.AxeCount;
            EnsureRenderers(_count);
            for (int i = 0; i < _axes.Count; i++)
            {
                SpriteRenderer axe = _axes[i];
                axe.gameObject.SetActive(i < _count);
                if (i >= _count) continue;

                Vector3 gap = front.position + forward * DistanceOf(i);
                axe.transform.SetPositionAndRotation(gap - facing * (GapLocal * scale), facing);
                axe.transform.localScale = Vector3.one * scale;
                axe.sortingOrder = -(i + 1); // 가까운 쌍이 위에 그려지게. 명중 이펙트(10)보다는 아래.
                _farGap = gap;
            }
        }

        public void Hide()
        {
            _count = 0;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 가까운 쌍부터 조준선이 구멍 안을 지나는지 본다. 다 지나면 통과(hit),
        /// 막히면 그 쌍의 평면에서 멈춘 것으로 본다(impact = 화살이 닿은 자리).
        /// </summary>
        public bool TryHit(Vector3 origin, Vector3 direction, out bool hit, out Vector3 impact)
        {
            hit = false;
            impact = default;
            var ray = new Ray(origin, direction);
            for (int i = 0; i < _count; i++)
            {
                Transform axe = _axes[i].transform;
                if (!new Plane(axe.forward, axe.position).Raycast(ray, out float distance)) return i > 0;
                impact = ray.GetPoint(distance);
                if (!IsInGap(axe.InverseTransformPoint(impact))) return true;
            }
            hit = _count > 0;
            return hit;
        }

        /// <summary>구멍을 통과하면 화살이 지나가고, 막히면 그 도끼에 꽂힌다.</summary>
        public bool ArrowSticks(bool hit) => !hit;

        /// <summary>
        /// 그림 위의 점(크기 1 기준)이 구멍 안인지. 판정 배율이 1보다 크면 구멍 기준점을 중심으로 그만큼 너그럽게 본다.
        /// </summary>
        private bool IsInGap(Vector2 local)
        {
            Vector2 center = GapLocal;
            Vector2 sample = center + (local - center) / _config.AxeHoleHitScale;
            if (_gapMask == null)
                return Vector2.Distance(local, center) <= _gapRadius01 * _axeSprite.bounds.size.y * _config.AxeHoleHitScale;

            Rect rect = _axeSprite.rect;
            Bounds b = _axeSprite.bounds;
            int x = Mathf.FloorToInt((sample.x - b.min.x) / b.size.x * rect.width);
            int y = Mathf.FloorToInt((sample.y - b.min.y) / b.size.y * rect.height);
            int w = (int)rect.width;
            return x >= 0 && y >= 0 && x < w && y < (int)rect.height && _gapMask[y * w + x];
        }

        /// <summary>
        /// 구멍 기준점에서 시작해 투명한 픽셀만 따라 채워 구멍 영역을 만든다.
        /// 텍스처를 읽을 수 없거나, 기준점이 도끼 위거나, 채우다 그림 가장자리에 닿으면(구멍이 막혀 있지 않음) 원 판정을 쓴다.
        /// </summary>
        private void BuildGapMask()
        {
            _gapMaskBuilt = true;
            Texture2D texture = _axeSprite.texture;
            if (!texture.isReadable)
            {
                Debug.LogWarning("[AxeRow] 도끼 그림 텍스처의 Read/Write가 꺼져 있어 원으로 판정합니다.");
                return;
            }

            Rect rect = _axeSprite.rect;
            int w = (int)rect.width, h = (int)rect.height, x0 = (int)rect.x, y0 = (int)rect.y;
            Color32[] pixels = texture.GetPixels32();
            bool Open(int x, int y) => pixels[(y0 + y) * texture.width + x0 + x].a < OpaqueAlpha;

            int sx = Mathf.Clamp(Mathf.FloorToInt(_gap01.x * w), 0, w - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(_gap01.y * h), 0, h - 1);
            if (!Open(sx, sy))
            {
                Debug.LogWarning("[AxeRow] 구멍 기준점이 도끼 위에 있어 원으로 판정합니다. _gap01을 구멍 안으로 옮겨 주세요.");
                return;
            }

            var mask = new bool[w * h];
            var stack = new Stack<int>();
            void Visit(int index)
            {
                if (mask[index] || !Open(index % w, index / w)) return;
                mask[index] = true;
                stack.Push(index);
            }

            Visit(sy * w + sx);
            while (stack.Count > 0)
            {
                int index = stack.Pop();
                int x = index % w, y = index / w;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                {
                    Debug.LogWarning("[AxeRow] 구멍이 그림 가장자리와 이어져 있어 원으로 판정합니다.");
                    return;
                }
                Visit(index - 1);
                Visit(index + 1);
                Visit(index - w);
                Visit(index + w);
            }
            _gapMask = mask;
        }

        private float DistanceOf(int index) => _config.AxeNearestMeters + index * _config.AxeSpacingMeters;

        /// <summary>구멍 기준점 위치(그림 기준점에서, 크기 1 기준).</summary>
        private Vector2 GapLocal
        {
            get
            {
                Bounds b = _axeSprite.bounds;
                return (Vector2)b.min + Vector2.Scale(b.size, _gap01);
            }
        }

        private void EnsureRenderers(int count)
        {
            while (_axes.Count < count)
            {
                var go = new GameObject($"AxePair{_axes.Count + 1}", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = _axeSprite;
                _axes.Add(renderer);
            }
        }
    }
}
