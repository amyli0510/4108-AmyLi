using EditorAttributes;
using UnityEngine;

public class Minigame_Food : Minigame_Base
{
    [Title("Food Settings")]
    [Tooltip("Points awarded for each good food item caught (or bad item avoided).")]
    [SerializeField] private int pointsPerCatch = 1;

    protected override void OnMinigameStarted()
    {
        // TODO: start spawning food, enable the catcher input.
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        // TODO: catching logic. On a successful catch:
        //   AddScore(pointsPerCatch);
    }

    protected override void OnMinigameEnded()
    {
        // TODO: stop spawning, show results. High score saves automatically.
    }
}
