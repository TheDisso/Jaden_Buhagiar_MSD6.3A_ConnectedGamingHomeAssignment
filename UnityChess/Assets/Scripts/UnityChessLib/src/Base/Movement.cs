using UnityEngine;
using Newtonsoft.Json;
using System;

/*
namespace UnityChess {
	/// <summary>Representation of a move, namely a piece and its end square.</summary>
	public class Movement {
		public readonly Square Start;
		public readonly Square End;

		/// <summary>Creates a new Movement.</summary>
		/// <param name="piecePosition">Position of piece being moved.</param>
		/// <param name="end">Square which the piece will land on.</param>
		public Movement(Square piecePosition, Square end) {
			Start = piecePosition;
			End = end;
		}

		/// <summary>Copy constructor.</summary>
		internal Movement(Movement move) {
			Start = move.Start;
			End = move.End;
		}
		
		protected bool Equals(Movement other) => Start == other.Start && End == other.End;

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return GetType() == obj.GetType() && Equals((Movement) obj);
		}

		public override int GetHashCode() {
			unchecked {
				return (Start.GetHashCode() * 397) ^ End.GetHashCode();
			}
		}

		public override string ToString() => $"{Start}->{End}";
	}
}
*/

namespace UnityChess
{
    /// <summary>
    /// Representation of a move, namely a piece and its end square.
    /// Now supports JSON serialization.
    /// </summary>
    [Serializable]
    public class Movement
    {
        public Square Start;
        public Square End;

        /// <summary>Default constructor (needed for JSON deserialization).</summary>
        public Movement() { }

        /// <summary>Creates a new Movement.</summary>
        /// <param name="piecePosition">Position of piece being moved.</param>
        /// <param name="end">Square which the piece will land on.</param>
        public Movement(Square piecePosition, Square end)
        {
            Start = piecePosition;
            End = end;
        }

        /// <summary>Copy constructor.</summary>
        public Movement(Movement move)
        {
            Start = move.Start;
            End = move.End;
        }

        /// <summary>Serializes this movement to JSON.</summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>Deserializes JSON into a Movement object.</summary>
        public static Movement FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Movement>(json);
        }

        protected bool Equals(Movement other) => Start == other.Start && End == other.End;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Movement movement && Equals(movement);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Start.GetHashCode() * 397) ^ End.GetHashCode();
            }
        }

        public override string ToString() => $"{Start}->{End}";
    }
}
