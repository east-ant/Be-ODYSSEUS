using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BeOdysseus
{
    /// <summary>게임 중 화면 위쪽 표시: 스테이지 번호, 남은 화살(금색=남음, 회색=씀), 남은 시간.</summary>
    public class StageHud : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _stageText;
        [Tooltip("화살 아이콘 하나. 화살 수만큼 복제해서 나란히 놓는다.")]
        [SerializeField] private Image _arrowTemplate;
        [SerializeField] private Sprite _arrowFull;
        [SerializeField] private Sprite _arrowUsed;
        [SerializeField] private Text _timeText;

        private readonly List<Image> _arrows = new();

        public void Show(StageRun run)
        {
            if (!_root.activeSelf) _root.SetActive(true);
            EnsureArrowCount(run.ArrowsTotal);

            _stageText.text = $"{run.StageNumber} STAGE";
            for (int i = 0; i < _arrows.Count; i++)
                _arrows[i].sprite = i < run.ArrowsLeft ? _arrowFull : _arrowUsed;
            _timeText.text = $"{Mathf.CeilToInt(run.TimeLeft)}초";
        }

        public void Hide() => _root.SetActive(false);

        private void EnsureArrowCount(int count)
        {
            // 에디터 화면 미리보기가 끝나면 복제한 화살을 지우므로, 지워진 것은 목록에서 뺀다.
            _arrows.RemoveAll(arrow => arrow == null);
            if (_arrows.Count == 0) _arrows.Add(_arrowTemplate);
            while (_arrows.Count < count)
                _arrows.Add(Instantiate(_arrowTemplate, _arrowTemplate.transform.parent));
            for (int i = 0; i < _arrows.Count; i++)
                _arrows[i].gameObject.SetActive(i < count);
        }
    }
}
