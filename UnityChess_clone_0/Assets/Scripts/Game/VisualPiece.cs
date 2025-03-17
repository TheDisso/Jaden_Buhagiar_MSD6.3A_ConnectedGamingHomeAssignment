using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

/// <summary>
/// Represents a visual chess piece in the game. This component handles user interaction,
/// such as dragging and dropping pieces, and determines the closest square on the board
/// where the piece should land. It also raises an event when a piece has been moved.
/// </summary>
public class VisualPiece : NetworkBehaviour {
    public delegate void VisualPieceMovedAction(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null);
    public static event VisualPieceMovedAction VisualPieceMoved;

    public Side PieceColor;

    public Square CurrentSquare
    {
        get
        {
            if (transform.parent == null)
            {
                Debug.LogError($"[VisualPiece] Parent missing for {gameObject.name}");
                return new Square(0, 0); // Return a default safe value instead of null
            }

            return StringToSquare(transform.parent.name);
        }
    }

    private const float SquareCollisionRadius = 9f;
    private Camera boardCamera;
    private Vector3 piecePositionSS;
    private List<GameObject> potentialLandingSquares;
    private Transform thisTransform;

    private void Start()
    {
        potentialLandingSquares = new List<GameObject>();
        thisTransform = transform;
        boardCamera = Camera.main;
    }
    public void OnMouseDown()
    {
        if (enabled)
        {
            // Convert the world position of the piece to screen-space and store it.
            piecePositionSS = boardCamera.WorldToScreenPoint(transform.position);
        }
    }

    private void OnMouseDrag()
    {
        if (IsOwner)
        {
            Vector3 nextPiecePositionSS = new Vector3(Input.mousePosition.x, Input.mousePosition.y, piecePositionSS.z);
            thisTransform.position = boardCamera.ScreenToWorldPoint(nextPiecePositionSS);
        }
    }

    public void OnMouseUp()
    {
        if (IsOwner) // Only the owner can move
        {
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

            VisualPieceMoved?.Invoke(CurrentSquare, thisTransform, closestSquareTransform);
        }
    }
}
