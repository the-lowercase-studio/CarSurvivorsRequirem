using Assets.Scripts.GameFlow;
using Assets.Scripts.ScoreBoard;
using Reflex.Core;
using UnityEngine;

namespace Assets.Scripts.ReflexDI
{
    public class ProjectInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
            //Scenes Loading
            builder.AddSingleton(typeof(GameSceneLoader), typeof(IGameSceneLoader));

            //ScoreBoard
            builder.AddSingleton(typeof(StoredScoreBoard));
            builder.AddSingleton(typeof(ScoreBoardNewScoreSaver), typeof(IScoreBoardNewScoreSaver));
            builder.AddSingleton(typeof(ScoreBoardBestScoreGetter), typeof(IScoreBoardBestScoreGetter));
        }
    }
}
