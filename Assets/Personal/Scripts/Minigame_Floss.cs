using EditorAttributes;
using UnityEngine;

public class Minigame_Floss : Minigame_Base
{
    [Title("Floss Settings")]
    [Tooltip("Points awarded for each gap successfully flossed.")]
    [SerializeField] private int pointsPerGap = 2;

    protected override void OnMinigameStarted()
    {
        // TODO: enable flossing input, set up the gaps between teeth.
    }

    protected override void OnMinigameTick(float deltaTime)
    {
        // TODO: flossing logic. When the player clears a gap:
        //   AddScore(pointsPerGap);
    }

    protected override void OnMinigameEnded()
    {
        // TODO: disable input, show results. High score saves automatically.
    }
}
