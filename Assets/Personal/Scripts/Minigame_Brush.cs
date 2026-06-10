using EditorAttributes;
using UnityEngine;

public class Minigame_Brush : Minigame_Base
{
    [Title("Brush Settings")]
    [Tooltip("Points awarded each time the player brushes a tooth correctly.")]
    [SerializeField] private int pointsPerStroke = 1;

    protected override void OnMinigameStarted()
    {
        // TODO: enable brushing input, spawn / reset teeth, show the brush cursor.
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        // TODO: brushing logic. When the player brushes a dirty spot:
        //   AddScore(pointsPerStroke);
    }

    protected override void OnMinigameEnded()
    {
        // TODO: disable input, play the results VFX. High score saves automatically.
    }
}
