using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using Newtonsoft.Json;
using static UnityChess.SquareUtil;
using System;

/// <summary>
/// Represents a visual chess piece in the game. Handles user interaction such as dragging and dropping,
/// detects valid board square targets, and sends validated move requests to the server.
/// </summary>
public class VisualPiece : NetworkBehaviour
{
    /// <summary>
    /// Delegate signature for when a piece is visually moved.
    /// </summary>
    /// <param name="movedPieceInitialSquare">The square the piece started on.</param>
    /// <param name="movedPieceTransform">The transform of the moved piece.</param>
    /// <param name="closestBoardSquareTransform">The transform of the destination square.</param>
    /// <param name="promotionPiece">Optional promotion piece if promotion occurs.</param>
    public delegate void VisualPieceMovedAction(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null);
    
    // Event invoked when a visual piece is dropped onto a square.
    public static event VisualPieceMovedAction VisualPieceMoved;

    private const float SquareCollisionRadius = 9f;
    private Camera boardCamera;
    private Vector3 piecePositionSS;
    private List<GameObject> potentialLandingSquares;
    private Transform thisTransform;

    // The color (side) this piece belongs to (White or Black).
    public Side PieceColor;

    /// <summary>
    /// Returns the square this piece is currently located on, based on its parent's name.
    /// </summary>
    public Square CurrentSquare
    {
        get
        {
            if (transform.parent == null)
            {
                Debug.LogError($"[VisualPiece] ERROR: {gameObject.name} has no parent!");
                return new Square(-1, -1);  // Use default invalid square
            }

            // Ensure the parent name is valid
            string squareName = transform.parent.name;
            if (string.IsNullOrEmpty(squareName))
            {
                Debug.LogError($"[VisualPiece] ERROR: {gameObject.name} parent name is invalid!");
                return new Square(-1, -1);
            }

            Square detectedSquare = SquareUtil.StringToSquare(squareName);
            Debug.Log($"[VisualPiece] {gameObject.name} is at {detectedSquare}");
            return detectedSquare;
        }
    }

    /// <summary>
    /// Initializes internal references when the piece is instantiated.
    /// </summary>
    private void Start()
    {
        potentialLandingSquares = new List<GameObject>();
        thisTransform = transform;
        boardCamera = Camera.main;
    }

    /// <summary>
    /// Called when the user begins to click on this piece.
    /// Performs turn validation and prepares for dragging.
    /// </summary>
    public void OnMouseDown()
    {
        Debug.Log($"[VisualPiece] Player {NetworkManager.Singleton.LocalClientId} clicked {PieceColor} at {CurrentSquare}. Owner: {IsOwner}");

        //if (!IsOwner)
        //{
        //    Debug.LogWarning($"[VisualPiece] Player {NetworkManager.Singleton.LocalClientId} tried moving {PieceColor}, but lacks ownership.");
        //    return;
        //}

        if (!GameManager.Instance.IsPlayerTurn() || PieceColor != GameManager.Instance.SideToMove)
        {
            Debug.LogWarning($"[VisualPiece] Player {NetworkManager.Singleton.LocalClientId} attempted to move {PieceColor}, but it's not their turn!");
            return;
        }

        if (enabled)
        {
            Debug.Log($"[VisualPiece] Player {NetworkManager.Singleton.LocalClientId} is moving {PieceColor} piece at {CurrentSquare}.");
            piecePositionSS = Camera.main.WorldToScreenPoint(transform.position);
        }
    }

    /// <summary>
    /// Called while the piece is being dragged by the mouse. Updates position in world space.
    /// </summary>
    private void OnMouseDrag()
    {
        if (!GameManager.Instance.IsPlayerTurn()) return;
        if (PieceColor != GameManager.Instance.SideToMove) return;

        if (enabled)
        {
            Vector3 nextPiecePositionSS = new Vector3(Input.mousePosition.x, Input.mousePosition.y, piecePositionSS.z);
            Vector3 finalPos = boardCamera.ScreenToWorldPoint(nextPiecePositionSS);
            MovePieceServerRpc(finalPos.x, finalPos.y, finalPos.z);
        }
    }

    /// <summary>
    /// Server RPC that updates the piece's position in world space during dragging.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void MovePieceServerRpc(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
    }

    /// <summary>
    /// Called when the player releases the mouse button.
    /// Snaps the piece to the closest valid square and sends move request to server.
    /// </summary>
    public void OnMouseUp()
    {
        if (!GameManager.Instance.IsPlayerTurn() || PieceColor != GameManager.Instance.SideToMove)
        {
            return;
        }

        potentialLandingSquares.Clear();
        BoardManager.Instance.GetSquareGOsWithinRadius(potentialLandingSquares, thisTransform.position, SquareCollisionRadius);

        if (potentialLandingSquares.Count == 0)
        {
            thisTransform.position = thisTransform.parent.position;
            return;
        }

        Transform closestSquareTransform = potentialLandingSquares[0].transform;
        float shortestDistanceFromPieceSquared = (closestSquareTransform.position - thisTransform.position).sqrMagnitude;

        for (int i = 1; i < potentialLandingSquares.Count; i++)
        {
            GameObject potentialLandingSquare = potentialLandingSquares[i];
            float distanceFromPieceSquared = (potentialLandingSquare.transform.position - thisTransform.position).sqrMagnitude;

            if (distanceFromPieceSquared < shortestDistanceFromPieceSquared)
            {
                shortestDistanceFromPieceSquared = distanceFromPieceSquared;
                closestSquareTransform = potentialLandingSquare.transform;
            }
        }

        // Serialize the movement object
        Movement move = new Movement(CurrentSquare, StringToSquare(closestSquareTransform.name));
        string moveJson = move.ToJson();

        // Instead of directly moving, send move request to the server
        RequestMoveServerRpc(moveJson);
    }

    /// <summary>
    /// Sends a JSON-serialized move request to the server.
    /// Server deserializes and validates the move before execution.
    /// </summary>
    /// <param name="moveJson">The serialized move in JSON format.</param>
    [ServerRpc(RequireOwnership = false)]
    private void RequestMoveServerRpc(string moveJson)
    {
        try
        {
            Movement move = JsonConvert.DeserializeObject<Movement>(moveJson);

            if (move == null)
            {
                Debug.LogError("[VisualPiece] Failed to deserialize moveJson. JSON might be incorrect.");
                return;
            }

            Debug.Log($"[VisualPiece] Received move request: {move.Start} -> {move.End}");

            GameManager.Instance.ExecuteMove(move.Start.File, move.Start.Rank, move.End.File, move.End.Rank);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VisualPiece] Error deserializing moveJson: {ex.Message}\nJSON: {moveJson}");
        }
    }
}
