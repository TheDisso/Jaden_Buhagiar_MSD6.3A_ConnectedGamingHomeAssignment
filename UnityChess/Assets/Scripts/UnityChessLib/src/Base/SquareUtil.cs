using System.Collections.Generic;
using UnityEngine; // Required for JsonUtility

/*
namespace UnityChess {
	public static class SquareUtil {
		public static readonly Dictionary<string, int> FileCharToIntMap = new() {
			{"a", 1},
			{"b", 2},
			{"c", 3},
			{"d", 4},
			{"e", 5},
			{"f", 6},
			{"g", 7},
			{"h", 8}
		};
		
		public static readonly Dictionary<int, string> FileIntToCharMap = new() {
			{1, "a"},
			{2, "b"},
			{3, "c"},
			{4, "d"},
			{5, "e"},
			{6, "f"},
			{7, "g"},
			{8, "h"}
		};
		
		public static readonly Square[] KnightOffsets = {
			new(-2, -1),
			new(-2, 1),
			new(2, -1),
			new(2, 1),
			new(-1, -2),
			new(-1, 2),
			new(1, -2),
			new(1, 2),
		};
		
		public static readonly Square[] SurroundingOffsets = {
			new(-1, 0),
			new(1, 0),
			new(0, -1),
			new(0, 1),
			new(-1, 1),
			new(-1, -1),
			new(1, -1),
			new(1, 1),
		};

		public static readonly Square[] DiagonalOffsets = {
			new(-1, 1),
			new(-1, -1),
			new(1, -1),
			new(1, 1)
		};
		
		public static readonly Square[] CardinalOffsets = {
			new(-1, 0),
			new(1, 0),
			new(0, -1),
			new(0, 1),
		};
		
		
	
		public static string SquareToString(Square square) => SquareToString(square.File, square.Rank);
		public static string SquareToString(int file, int rank) {
			if (FileIntToCharMap.TryGetValue(file, out string fileChar)) {
				return $"{fileChar}{rank}";
			}

			return "Invalid";
		}

		public static Square StringToSquare(string squareText) {
			return new Square(
				FileCharToIntMap[squareText[0].ToString()],
				int.Parse(squareText[1].ToString())
			);
		}
	}
}
*/

namespace UnityChess
{
    [System.Serializable]
    public static class SquareUtil
    {
        public static readonly Dictionary<string, int> FileCharToIntMap = new() {
            {"a", 1},
            {"b", 2},
            {"c", 3},
            {"d", 4},
            {"e", 5},
            {"f", 6},
            {"g", 7},
            {"h", 8}
        };

        public static readonly Dictionary<int, string> FileIntToCharMap = new() {
            {1, "a"},
            {2, "b"},
            {3, "c"},
            {4, "d"},
            {5, "e"},
            {6, "f"},
            {7, "g"},
            {8, "h"}
        };

        public static readonly Square[] KnightOffsets = {
            new(-2, -1),
            new(-2, 1),
            new(2, -1),
            new(2, 1),
            new(-1, -2),
            new(-1, 2),
            new(1, -2),
            new(1, 2),
        };

        public static readonly Square[] SurroundingOffsets = {
            new(-1, 0),
            new(1, 0),
            new(0, -1),
            new(0, 1),
            new(-1, 1),
            new(-1, -1),
            new(1, -1),
            new(1, 1),
        };

        public static readonly Square[] DiagonalOffsets = {
            new(-1, 1),
            new(-1, -1),
            new(1, -1),
            new(1, 1)
        };

        public static readonly Square[] CardinalOffsets = {
            new(-1, 0),
            new(1, 0),
            new(0, -1),
            new(0, 1),
        };

        public static string SquareToString(Square square) => SquareToString(square.File, square.Rank);

        public static string SquareToString(int file, int rank)
        {
            if (FileIntToCharMap.TryGetValue(file, out string fileChar))
            {
                return $"{fileChar}{rank}";
            }

            return "Invalid";
        }

        public static Square StringToSquare(string squareText)
        {
            if (string.IsNullOrEmpty(squareText) || squareText.Length < 2)
            {
                return Square.Invalid;
            }

            return new Square(
                FileCharToIntMap[squareText[0].ToString()],
                int.Parse(squareText[1].ToString())
            );
        }

        /// <summary>
        /// Serializes SquareUtil data to JSON.
        /// </summary>
        public static string ToJson() => JsonUtility.ToJson(new SerializableSquareUtil());

        /// <summary>
        /// Deserializes JSON to SquareUtil data.
        /// </summary>
        public static void FromJson(string json)
        {
            SerializableSquareUtil data = JsonUtility.FromJson<SerializableSquareUtil>(json);
            if (data != null)
            {
                FileCharToIntMap.Clear();
                foreach (var pair in data.FileCharToIntMap)
                {
                    FileCharToIntMap[pair.Key] = pair.Value;
                }
            }
        }
    }

    /// <summary>
    /// Serializable wrapper for SquareUtil
    /// </summary>
    [System.Serializable]
    public class SerializableSquareUtil
    {
        public Dictionary<string, int> FileCharToIntMap;

        public SerializableSquareUtil()
        {
            FileCharToIntMap = new Dictionary<string, int>(SquareUtil.FileCharToIntMap);
        }
    }
}
