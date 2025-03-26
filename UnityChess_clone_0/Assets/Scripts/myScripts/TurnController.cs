using UnityEngine;
using Unity.Netcode;
using UnityChess;

public class TurnController : MonoBehaviourSingleton<TurnController>
{
    /// <summary>
    /// Returns true if the current turn is White.
    /// </summary>
    public bool SideToMoveIsWhite()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("TurnController: GameManager instance not found.");
            return false;
        }
        return GameManager.Instance.SideToMove == Side.White;
    }

    /// <summary>
    /// Determines if the specified client (by id) is the one whose turn it currently is.
    /// Assumes that the first connected player (index 0) is White and the second (index 1) is Black.
    /// </summary>
    public bool IsClientTurn(ulong clientId)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("TurnController: GameManager instance not found.");
            return false;
        }
        if (GameManager.Instance.PlayersConnected.Count < 2)
        {
            Debug.LogWarning("TurnController: Not enough players connected.");
            return false;
        }
        return SideToMoveIsWhite() ? clientId == GameManager.Instance.PlayersConnected[0] : clientId == GameManager.Instance.PlayersConnected[1];
    }
}
