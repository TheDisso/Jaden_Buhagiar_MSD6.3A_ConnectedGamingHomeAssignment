using System.Collections;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityChess;

public class JsonPieceConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(Piece).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // If the token is a string, assume it is a short identifier.
        if (reader.TokenType == JsonToken.String)
        {
            string pieceStr = (string)reader.Value;
            // You need to define a mapping from the string to a concrete Piece.
            // For instance, if the string contains "Pawn":
            if (pieceStr.Contains("Pawn"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new Pawn(owner);
            }
            else if (pieceStr.Contains("Rook"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new Rook(owner);
            }
            else if (pieceStr.Contains("Knight"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new Knight(owner);
            }
            else if (pieceStr.Contains("Bishop"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new Bishop(owner);
            }
            else if (pieceStr.Contains("Queen"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new Queen(owner);
            }
            else if (pieceStr.Contains("King"))
            {
                Side owner = pieceStr.Contains("Black") ? Side.Black : Side.White;
                return new King(owner);
            }
            else
            {
                throw new JsonSerializationException($"Unknown piece identifier: {pieceStr}");
            }
        }
        else
        {
            // Otherwise, let the serializer handle it normally.
            return serializer.Deserialize(reader, objectType);
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // Here we just serialize the piece as a full object with type info.
        serializer.Serialize(writer, value);
    }
}
