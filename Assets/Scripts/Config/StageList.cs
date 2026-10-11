using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>스테이지 하나에 쓰는 그림.</summary>
    [Serializable]
    public class StageDefinition
    {
        [Tooltip("이 스테이지 몬스터의 고해상도 그림. 결과 화면 오른쪽에 크게 쓰고, 애니메이션이 없으면 게임 중에도 이 그림을 쓴다.")]
        [SerializeField] private Sprite _monsterSprite;
        [Tooltip("게임 중 몬스터 애니메이션(대기·걷기·쓰러짐). 비워 두면 위 그림 한 장으로 움직인다.")]
        [SerializeField] private MonsterAnimationSet _monsterAnimation;
        [Tooltip("결과 화면 배경.")]
        [SerializeField] private Sprite _resultBackground;
        [Tooltip("태블릿에 보내는 몬스터 이름(docs/TABLET_LINK_PROTOCOL.md의 monster 값). 예: polyphemus, shade, siren")]
        [SerializeField] private string _monsterId;
        [Tooltip("이 스테이지 몬스터까지 거리(m). 가까울수록 크게 보인다. 0이면 GameConfig의 Area Distance Meters를 쓴다.")]
        [SerializeField, Min(0f)] private float _monsterDistanceMeters;

        public Sprite MonsterSprite => _monsterSprite;
        /// <summary>몬스터 거리(m). 0이면 기본 거리.</summary>
        public float MonsterDistanceMeters => _monsterDistanceMeters;
        public string MonsterId => _monsterId;
        public MonsterAnimationSet MonsterAnimation => _monsterAnimation;
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
