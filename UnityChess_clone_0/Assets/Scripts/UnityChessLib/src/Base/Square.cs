using System;
using UnityEngine;
using Newtonsoft.Json;

/*
namespace UnityChess {
	/// <summary>Representation of a square on a chessboard.</summary>
	public readonly struct Square {
		public static readonly Square Invalid = new Square(-1, -1);
		public readonly int File;
		public readonly int Rank;

		/// <summary>Creates a new Square instance.</summary>
		/// <param name="file">Column of the square.</param>
		/// <param name="rank">Row of the square.</param>
		public Square(int file, int rank) {
			File = file;
			Rank = rank;
		}

		public Square(string squareString) {
			this = string.IsNullOrEmpty(squareString)
				? Invalid
				: SquareUtil.StringToSquare(squareString);
		}

		internal Square(Square startPosition, int fileOffset, int rankOffset) {
			File = startPosition.File + fileOffset;
			Rank = startPosition.Rank + rankOffset;
		}
		
		internal readonly bool IsValid() {
			return File is >= 1 and <= 8
			       && Rank is >= 1 and <= 8;
		}

		public static bool operator ==(Square lhs, Square rhs) => lhs.File == rhs.File && lhs.Rank == rhs.Rank;
		public static bool operator !=(Square lhs, Square rhs) => !(lhs == rhs);
		public static Square operator +(Square lhs, Square rhs) => new Square(lhs.File + rhs.File, lhs.Rank + rhs.Rank);
		
		public bool Equals(Square other) => File == other.File && Rank == other.Rank;

		public bool Equals(int file, int rank) => File == file && Rank == rank;

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;

			return obj is Square other && Equals(other);
		}

		public override int GetHashCode() {
			unchecked {
				return (File * 397) ^ Rank;
			}
		}

		public override string ToString() => SquareUtil.SquareToString(this);
	}
}
*/

namespace UnityChess
{
    /// <summary>Representation of a square on a chessboard.</summary>
    [Serializable]
    public class Square
    {
        public static readonly Square Invalid = new Square(-1, -1);

        [JsonProperty] // Ensure proper JSON serialization
        public int File { get; set; }

        [JsonProperty]
        public int Rank { get; set; }

        /// <summary>Default constructor (needed for JSON deserialization).</summary>
        public Square()
        {
            File = -1;
            Rank = -1;
        }

        /// <summary>Creates a new Square instance.</summary>
        public Square(int file, int rank)
        {
            File = file;
            Rank = rank;
        }

        public Square(string squareString)
        {
            if (string.IsNullOrEmpty(squareString))
            {
                File = Invalid.File;
                Rank = Invalid.Rank;
            }
            else
            {
                Square temp = SquareUtil.StringToSquare(squareString);
                File = temp.File;
                Rank = temp.Rank;
            }
        }

        internal Square(Square startPosition, int fileOffset, int rankOffset)
        {
            File = startPosition.File + fileOffset;
            Rank = startPosition.Rank + rankOffset;
        }

        internal bool IsValid()
        {
            return File is >= 1 and <= 8
                   && Rank is >= 1 and <= 8;
        }

        public static bool operator ==(Square lhs, Square rhs) =>
            lhs is not null && rhs is not null && lhs.File == rhs.File && lhs.Rank == rhs.Rank;

        public static bool operator !=(Square lhs, Square rhs) => !(lhs == rhs);

        public static Square operator +(Square lhs, Square rhs) =>
            new Square(lhs.File + rhs.File, lhs.Rank + rhs.Rank);

        public override bool Equals(object obj) =>
            obj is Square other && File == other.File && Rank == other.Rank;

        public override int GetHashCode() => (File * 397) ^ Rank;

        public override string ToString() => SquareUtil.SquareToString(this);

        /// <summary>Serializes this Square to JSON.</summary>
        public string ToJson() => JsonConvert.SerializeObject(this);

        /// <summary>Deserializes a JSON string into a Square object.</summary>
        public static Square FromJson(string json) => JsonConvert.DeserializeObject<Square>(json);
    }
}
