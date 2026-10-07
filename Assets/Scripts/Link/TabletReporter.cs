using System;
using UnityEngine;

namespace BeOdysseus
{
    /// <summary>
    /// 게임 진행을 태블릿에 알린다: 게임 시작, 스테이지 시작, 발사 결과, 스테이지 결과, 게임 끝.
    /// 메시지 모양은 docs/TABLET_LINK_PROTOCOL.md 4장. 연결한 태블릿이 없으면 보내지 않는다.
    /// </summary>
    public class TabletReporter : MonoBehaviour
    {
        [SerializeField] private TabletLink _link;
        [SerializeField] private GameFlow _game;

        private string _gameId = "";

        private void OnEnable()
        {
            _game.GameStarted += OnGameStarted;
            _game.StageStarted += OnStageStarted;
            _game.ShotRecorded += OnShotRecorded;
            _game.StageFinished += OnStageFinished;
            _game.GameEnded += OnGameEnded;
        }

        private void OnDisable()
        {
            _game.GameStarted -= OnGameStarted;
            _game.StageStarted -= OnStageStarted;
            _game.ShotRecorded -= OnShotRecorded;
            _game.StageFinished -= OnStageFinished;
            _game.GameEnded -= OnGameEnded;
        }

        private void OnGameStarted()
        {
            _gameId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _link.SendReliable(new GameStartMessage { type = LinkProtocol.Types.GameStart, gameId = _gameId });
        }

        private void OnStageStarted(StageRun run, StageDefinition stage)
        {
            _link.SendReliable(new StageStartMessage
            {
                type = LinkProtocol.Types.StageStart,
                gameId = _gameId,
                stage = run.StageNumber,
                monster = stage.MonsterId,
            });
        }

        private void OnShotRecorded(ShotResult shot, StageRun run, bool killed)
        {
            _link.SendReliable(new ShotMessage
            {
                type = LinkProtocol.Types.Shot,
                gameId = _gameId,
                stage = run.StageNumber,
                shotIndex = run.Shots.Count,
                hit = shot.Hit,
                killed = killed,
                accuracy = LinkProtocol.Round3(shot.Accuracy01),
                stability = LinkProtocol.Round3(shot.Stability01),
            });
        }

        private void OnStageFinished(StageResult result)
        {
            _link.SendReliable(new StageEndMessage
            {
                type = LinkProtocol.Types.StageEnd,
                gameId = _gameId,
                stage = result.StageNumber,
                cleared = result.Cleared,
                totalScore = result.TotalScore,
                totalHits = result.TotalHits,
                totalShots = result.TotalShots,
                hitRate = LinkProtocol.Round3(result.HitRate01),
                avgAccuracy = LinkProtocol.Round3(result.AverageAccuracy01),
                avgStability = LinkProtocol.Round3(result.AverageStability01),
            });
        }

        private void OnGameEnded(bool completed, StageResult totals)
        {
            _link.SendReliable(new GameEndMessage
            {
                type = LinkProtocol.Types.GameEnd,
                gameId = _gameId,
                completed = completed,
                totalScore = totals.TotalScore,
                totalHits = totals.TotalHits,
                totalShots = totals.TotalShots,
                hitRate = LinkProtocol.Round3(totals.HitRate01),
                avgAccuracy = LinkProtocol.Round3(totals.AverageAccuracy01),
                avgStability = LinkProtocol.Round3(totals.AverageStability01),
            });
        }
    }
}
