using System.Collections;
using System.Collections.Generic;
using UnityChess;
using UnityEngine;

[System.Serializable]
public class FirebasePieceData
{
    public string square;
    public string type;
    public string owner;

    public FirebasePieceData() { }

    public FirebasePieceData(Square square, Piece piece)
    {
        this.square = square.ToString(); // e.g., "e4"
        this.type = piece.GetType().Name; // e.g., "Rook"
        this.owner = piece.Owner.ToString(); // "White" or "Black"
    }
}
