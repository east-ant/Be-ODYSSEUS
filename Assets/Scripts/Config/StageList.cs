using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>스테이지 하나에 쓰는 그림.</summary>
    [Serializable]
    public class StageDefinition
    {
        [Tooltip("이 스테이지에 나오는 몬스터 이미지. 게임 중과 결과 화면 오른쪽에 같이 쓴다.")]
        [SerializeField] private Sprite _monsterSprite;
        [Tooltip("결과 화면 배경.")]
        [SerializeField] private Sprite _resultBackground;

        public Sprite MonsterSprite => _monsterSprite;
        public Sprite ResultBackground => _resultBackground;
    }

    /// <summary>순서대로 진행할 스테이지 목록. 스테이지를 늘리려면 여기에 항목을 추가한다.</summary>
    [CreateAssetMenu(fileName = "Stages", menuName = "Be ODYSSEUS/Stage List")]
    public class StageList : ScriptableObject
    {
        [SerializeField] private List<StageDefinition> _stages = new();

        public int Count => _stages.Count;
        public StageDefinition Get(int index) => _stages[index];
    }
}
