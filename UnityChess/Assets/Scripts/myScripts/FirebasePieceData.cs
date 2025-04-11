using System.Collections;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;

/// <summary>
/// Serializable data model for saving a chess piece and its position
/// in a format suitable for Firebase or JSON storage.
/// </summary>
[System.Serializable]
public class FirebasePieceData
{
    /// <summary>
    /// The square the piece occupies (e.g., "e4").
    /// </summary>
    public string square;

    /// <summary>
    /// The type of the piece (e.g., "Rook", "Pawn").
    /// </summary>
    public string type;

    /// <summary>
    /// The owner of the piece ("White" or "Black").
    /// </summary>
    public string owner;

    /// <summary>
    /// Default constructor required for deserialization.
    /// </summary>
    public FirebasePieceData() { }

    /// <summary>
    /// Constructs a FirebasePieceData instance from a Square and a Piece.
    /// </summary>
    /// <param name="square">The position of the piece on the board.</param>
    /// <param name="piece">The chess piece to serialize.</param>
    public FirebasePieceData(Square square, Piece piece)
    {
        this.square = square.ToString();
        this.type = piece.GetType().Name;
        this.owner = piece.Owner.ToString();
    }
}
