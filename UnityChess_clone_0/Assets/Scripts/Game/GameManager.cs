using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using static UnityChess.SquareUtil;

/// <summary>
/// Manages the overall game state, including game start, moves execution,
/// special moves handling (such as castling, en passant, and promotion), and game reset.
/// Inherits from a singleton base class to ensure a single instance throughout the application.
/// </summary>
public class GameManager : NetworkBehaviourSingleton<GameManager>
{
    /*    public static event Action NewGameStartedEvent;
        public static event Action GameEndedEvent;
        public static event Action GameResetToHalfMoveEvent;
        public static event Action MoveExecutedEvent;

        private NetworkVariable<bool> isWhiteTurn = new NetworkVariable<bool>(true);
        private Dictionary<ulong, Side> playerSides = new Dictionary<ulong, Side>();
        private Game game;

        private CancellationTokenSource promotionUITaskCancellationTokenSource;
        private ElectedPiece userPromotionChoice = ElectedPiece.None;
        private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
        private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

        public Board CurrentBoard
        {
            get
            {
                game.BoardTimeline.TryGetCurrent(out Board currentBoard);
                return currentBoard;
            }
        }

        public Side SideToMove => isWhiteTurn.Value ? Side.White : Side.Black;
        /// <summary>
        /// Gets the side that started the game.
        /// </summary>
        public Side StartingSide => game.ConditionsTimeline[0].SideToMove;

        public int FullMoveNumber => StartingSide switch
        {
            Side.White => LatestHalfMoveIndex / 2 + 1,
            Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
            _ => -1
        };


        public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
        public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;

        private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                AssignPlayerServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }

        public List<(Square, Piece)> CurrentPieces
        {
            get
            {
                currentPiecesBacking.Clear();
                for (int file = 1; file <= 8; file++)
                {
                    for (int rank = 1; rank <= 8; rank++)
                    {
                        Piece piece = CurrentBoard[file, rank];
                        if (piece != null) currentPiecesBacking.Add((new Square(file, rank), piece));
                    }
                }
                return currentPiecesBacking;
            }
        }
        /// <summary>
        /// Unity's Start method initializes event handlers and game setup.
        /// </summary>
        private void Start()
        {
            if (IsServer)
            {
                playerSides.Clear();
            }

            VisualPiece.VisualPieceMoved += OnPieceMoved; // Fixing missing reference

            serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
            {
                [GameSerializationType.FEN] = new FENSerializer(),
                [GameSerializationType.PGN] = new PGNSerializer()
            };

            if (IsServer)
            {
                StartNewGame();
            }
        }

        /// <summary>
        /// Assigns players to White or Black.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void AssignPlayerServerRpc(ulong playerId)
        {
            if (!playerSides.ContainsKey(playerId))
            {
                playerSides[playerId] = playerSides.Count == 0 ? Side.White : Side.Black;
            }
        }

        /// <summary>
        /// Checks if the requesting player is allowed to move.
        /// </summary>
        private bool IsMoveAllowed(ulong playerId)
        {
            return playerSides.TryGetValue(playerId, out Side side) && side == SideToMove;
        }

        /// <summary>
        /// Handles player move requests.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestMoveServerRpc(Vector2Int from, Vector2Int to, ulong playerId)
        {
            if (!IsMoveAllowed(playerId)) return; // Enforce turn order

            if (game.TryGetLegalMove(new Square(from.x, from.y), new Square(to.x, to.y), out Movement move))
            {
                if (TryExecuteMove(move))
                {
                    isWhiteTurn.Value = !isWhiteTurn.Value; // Switch turns
                    SyncBoardStateClientRpc(SerializeGame()); // Send board update
                }
            }
        }

        /// <summary>
        /// Synchronizes the board state for all clients.
        /// </summary>
        [ClientRpc]
        private void SyncBoardStateClientRpc(string fenState)
        {
            LoadGame(fenState);
        }

        /// <summary>
        /// Executes a given move.
        /// </summary>
        private bool TryExecuteMove(Movement move)
        {
            if (!game.TryExecuteMove(move)) return false;

            HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

            if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
            {
                GameOverClientRpc(latestHalfMove.CausedCheckmate ? $"{SideToMove.Complement()} Wins!" : "Draw.");
            }

            MoveExecutedEvent?.Invoke();
            return true;
        }

        /// <summary>
        /// Notifies clients when the game ends.
        /// </summary>
        [ClientRpc]
        private void GameOverClientRpc(string message)
        {
            UIManager.Instance.DisplayGameOverMessage(message);
        }

        /// <summary>
        /// Starts a new game on the server and syncs it with all clients.
        /// </summary>
        public void StartNewGame()
        {
            if (!IsServer) return;

            game = new Game();
            isWhiteTurn.Value = true;
            NewGameStartedEvent?.Invoke();
            SyncBoardStateClientRpc(SerializeGame());
        }

        /// <summary>
        /// Serializes the current game state.
        /// </summary>
        public string SerializeGame()
        {
            return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
                ? serializer?.Serialize(game)
                : null;
        }

        /// <summary>
        /// Loads a game from a serialized game state string.
        /// </summary>
        public void LoadGame(string serializedGame)
        {
            game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
            NewGameStartedEvent?.Invoke();
        }

        /// <summary>
        /// Fixing the missing `OnPieceMoved` method.
        /// </summary>
        private void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
        {
            Vector2Int from = new Vector2Int(movedPieceInitialSquare.File, movedPieceInitialSquare.Rank);
            Vector2Int to = new Vector2Int(StringToSquare(closestBoardSquareTransform.name).File, StringToSquare(closestBoardSquareTransform.name).Rank);

            RequestMoveServerRpc(from, to, NetworkManager.Singleton.LocalClientId);
        }

        /// <summary>
        /// Resets the game to a specific half-move index.
        /// </summary>
        public void ResetGameToHalfMoveIndex(int halfMoveIndex)
        {
            if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

            UIManager.Instance.SetActivePromotionUI(false);
            promotionUITaskCancellationTokenSource?.Cancel();
            GameResetToHalfMoveEvent?.Invoke();
        }

        /// <summary>
        /// Handles special move behavior.
        /// </summary>
        private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
        {
            switch (specialMove)
            {
                case CastlingMove castlingMove:
                    BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                    return true;
                case EnPassantMove enPassantMove:
                    BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                    return true;
                case PromotionMove { PromotionPiece: null } promotionMove:
                    UIManager.Instance.SetActivePromotionUI(true);
                    BoardManager.Instance.SetActiveAllPieces(false);

                    promotionUITaskCancellationTokenSource?.Cancel();
                    promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                    ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);

                    UIManager.Instance.SetActivePromotionUI(false);
                    BoardManager.Instance.SetActiveAllPieces(true);

                    if (promotionUITaskCancellationTokenSource == null || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        return false;
                    }

                    promotionMove.SetPromotionPiece(PromotionUtil.GeneratePromotionPiece(choice, SideToMove));
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                    BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                    promotionUITaskCancellationTokenSource = null;
                    return true;
                case PromotionMove promotionMove:
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                    BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                    BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                    return true;
                default:
                    return false;
            }
        }

        private ElectedPiece GetUserPromotionPieceChoice()
        {
            while (userPromotionChoice == ElectedPiece.None) { }
            ElectedPiece result = userPromotionChoice;
            userPromotionChoice = ElectedPiece.None;
            return result;
        }

        public void ElectPiece(ElectedPiece choice)
        {
            userPromotionChoice = choice;
        }
        public bool HasLegalMoves(Piece piece)
        {
            return game.TryGetLegalMovesForPiece(piece, out _);
        }*/

    // Events signalling various game state changes.
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;

    /// <summary>
    /// Gets the current board state from the game.
    /// </summary>
    public Board CurrentBoard
    {
        get
        {
            // Attempts to retrieve the current board from the board timeline.
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    /// <summary>
    /// Gets the side (White/Black) whose turn it is to move.
    /// </summary>
    public Side SideToMove
    {
        get
        {
            // Retrieves the current game conditions and returns the active side.
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            return currentConditions.SideToMove;
        }
    }

    /// <summary>
    /// Gets the side that started the game.
    /// </summary>
    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;

    /// <summary>
    /// Gets the timeline of half-moves made in the game.
    /// </summary>
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;

    /// <summary>
    /// Gets the index of the most recent half-move.
    /// </summary>
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;

    /// <summary>
    /// Computes the full move number based on the starting side and the latest half-move index.
    /// </summary>
    public int FullMoveNumber => StartingSide switch
    {
        Side.White => LatestHalfMoveIndex / 2 + 1,
        Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
        _ => -1
    };

    private bool isWhiteAI;
    private bool isBlackAI;

    /// <summary>
    /// Gets a list of all current pieces on the board, along with their positions.
    /// </summary>
    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            // Clear the backing list before populating with current pieces.
            currentPiecesBacking.Clear();
            // Iterate over every square on the board.
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = CurrentBoard[file, rank];
                    // If a piece exists at this position, add it to the list.
                    if (piece != null) currentPiecesBacking.Add((new Square(file, rank), piece));
                }
            }
            return currentPiecesBacking;
        }
    }

    // Backing list for storing current pieces on the board.
    private readonly List<(Square, Piece)> currentPiecesBacking = new List<(Square, Piece)>();

    // Reference to the debug utility for the chess engine.
    [SerializeField] private UnityChessDebug unityChessDebug;
    // The current game instance.
    private Game game;
    // Serializers for game state (FEN and PGN formats).
    private FENSerializer fenSerializer;
    private PGNSerializer pgnSerializer;
    // Cancellation token source for asynchronous promotion UI tasks.
    private CancellationTokenSource promotionUITaskCancellationTokenSource;
    // Stores the user's choice for promotion; initialised to none.
    private ElectedPiece userPromotionChoice = ElectedPiece.None;
    // Mapping of game serialization types to their corresponding serializers.
    private Dictionary<GameSerializationType, IGameSerializer> serializersByType;
    // Currently selected serialization type (default is FEN).
    private GameSerializationType selectedSerializationType = GameSerializationType.FEN;

    /// <summary>
    /// Unity's Start method initialises the game and sets up event handlers.
    /// </summary>
    public void Start()
    {
        // Subscribe to the event triggered when a visual piece is moved.
        VisualPiece.VisualPieceMoved += OnPieceMoved;

        // Initialise the serializers for FEN and PGN formats.
        serializersByType = new Dictionary<GameSerializationType, IGameSerializer>
        {
            [GameSerializationType.FEN] = new FENSerializer(),
            [GameSerializationType.PGN] = new PGNSerializer()
        };

        // Begin a new game.
        StartNewGame();

#if DEBUG_VIEW
		// Enable debug view if compiled with DEBUG_VIEW flag.
		unityChessDebug.gameObject.SetActive(true);
		unityChessDebug.enabled = true;
#endif
    }

    /// <summary>
    /// Starts a new game by creating a new game instance and invoking the NewGameStartedEvent.
    /// </summary>
    public async void StartNewGame()
    {
        if (IsServer)
        {
            game = new Game();
            //AssignRoles();
            NewGameStartedEvent?.Invoke();
        }
    }

    /*private void AssignRoles()
    {
        if (IsServer)
        {
            // The host (server) is always White
            NetworkManager.Singleton.ConnectedClients[NetworkManager.Singleton.LocalClientId].PlayerObject.GetComponent<PlayerData>().side = Side.White;
        }

        if (IsClient)
        {
            // The client is always Black
            NetworkManager.Singleton.ConnectedClients[NetworkManager.Singleton.LocalClientId].PlayerObject.GetComponent<PlayerData>().side = Side.Black;
        }
    }*/

    /// <summary>
    /// Serialises the current game state using the selected serialization format.
    /// </summary>
    /// <returns>A string representing the serialised game state.</returns>
    public string SerializeGame()
    {
        return serializersByType.TryGetValue(selectedSerializationType, out IGameSerializer serializer)
            ? serializer?.Serialize(game)
            : null;
    }

    /// <summary>
    /// Loads a game from the given serialised game state string.
    /// </summary>
    /// <param name="serializedGame">The serialised game state string.</param>
    public void LoadGame(string serializedGame)
    {
        game = serializersByType[selectedSerializationType].Deserialize(serializedGame);
        NewGameStartedEvent?.Invoke();
    }

    /// <summary>
    /// Resets the game to a specific half-move index.
    /// </summary>
    /// <param name="halfMoveIndex">The target half-move index to reset the game to.</param>
    public void ResetGameToHalfMoveIndex(int halfMoveIndex)
    {
        // If the reset operation fails, exit early.
        if (!game.ResetGameToHalfMoveIndex(halfMoveIndex)) return;

        // Disable promotion UI and cancel any pending promotion tasks.
        UIManager.Instance.SetActivePromotionUI(false);
        promotionUITaskCancellationTokenSource?.Cancel();
        // Notify subscribers that the game has been reset to a half-move.
        GameResetToHalfMoveEvent?.Invoke();
    }

    /// <summary>
    /// Attempts to execute a given move in the game.
    /// </summary>
    /// <param name="move">The move to execute.</param>
    /// <returns>True if the move was successfully executed; otherwise, false.</returns>
    private bool TryExecuteMove(Movement move)
    {
        // Attempt to execute the move within the game logic.
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        // Retrieve the latest half-move from the timeline.
        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        // If the latest move resulted in checkmate or stalemate, disable further moves.
        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else
        {
            // Otherwise, ensure that only the pieces of the side to move are enabled.
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        // Signal that a move has been executed.
        MoveExecutedEvent?.Invoke();

        return true;
    }

    /// <summary>
    /// Handles special move behaviour asynchronously (castling, en passant, and promotion).
    /// </summary>
    /// <param name="specialMove">The special move to process.</param>
    /// <returns>A task that resolves to true if the special move was handled; otherwise, false.</returns>
    private async Task<bool> TryHandleSpecialMoveBehaviourAsync(SpecialMove specialMove)
    {
        switch (specialMove)
        {
            // Handle castling move.
            case CastlingMove castlingMove:
                BoardManager.Instance.CastleRook(castlingMove.RookSquare, castlingMove.GetRookEndSquare());
                return true;
            // Handle en passant move.
            case EnPassantMove enPassantMove:
                BoardManager.Instance.TryDestroyVisualPiece(enPassantMove.CapturedPawnSquare);
                return true;
            // Handle promotion move when no promotion piece has been selected yet.
            case PromotionMove { PromotionPiece: null } promotionMove:
                // Activate the promotion UI and disable all pieces.
                UIManager.Instance.SetActivePromotionUI(true);
                BoardManager.Instance.SetActiveAllPieces(false);

                // Cancel any pending promotion UI tasks.
                promotionUITaskCancellationTokenSource?.Cancel();
                promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                // Await user's promotion choice asynchronously.
                ElectedPiece choice = await Task.Run(GetUserPromotionPieceChoice, promotionUITaskCancellationTokenSource.Token);

                // Deactivate the promotion UI and re-enable all pieces.
                UIManager.Instance.SetActivePromotionUI(false);
                BoardManager.Instance.SetActiveAllPieces(true);

                // If the task was cancelled, return false.
                if (promotionUITaskCancellationTokenSource == null
                    || promotionUITaskCancellationTokenSource.Token.IsCancellationRequested
                ) { return false; }

                // Set the chosen promotion piece.
                promotionMove.SetPromotionPiece(
                    PromotionUtil.GeneratePromotionPiece(choice, SideToMove)
                );
                // Update the board visuals for the promotion.
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                promotionUITaskCancellationTokenSource = null;
                return true;
            // Handle promotion move when the promotion piece is already set.
            case PromotionMove promotionMove:
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);

                return true;
            // Default case: if the special move is not recognised.
            default:
                return false;
        }
    }

    /// <summary>
    /// Blocks until the user selects a piece for pawn promotion.
    /// </summary>
    /// <returns>The elected promotion piece chosen by the user.</returns>
    private ElectedPiece GetUserPromotionPieceChoice()
    {
        // Wait until the user selects a promotion piece.
        while (userPromotionChoice == ElectedPiece.None) { }

        ElectedPiece result = userPromotionChoice;
        // Reset the user promotion choice.
        userPromotionChoice = ElectedPiece.None;
        return result;
    }

    /// <summary>
    /// Allows the user to elect a promotion piece.
    /// </summary>
    /// <param name="choice">The elected promotion piece.</param>
    public void ElectPiece(ElectedPiece choice)
    {
        userPromotionChoice = choice;
    }

    /// <summary>
    /// Handles the event triggered when a visual chess piece is moved.
    /// This method validates the move, handles special moves, and updates the board state.
    /// </summary>
    /// <param name="movedPieceInitialSquare">The original square of the moved piece.</param>
    /// <param name="movedPieceTransform">The transform of the moved piece.</param>
    /// <param name="closestBoardSquareTransform">The transform of the closest board square.</param>
    /// <param name="promotionPiece">Optional promotion piece (used in pawn promotion).</param>
    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        // ✅ Restrict movement based on the turn and player role
        if ((SideToMove == Side.White && !IsClient) || (SideToMove == Side.Black && !IsHost))
        {
            Debug.LogWarning("Not your turn!");
            movedPieceTransform.position = movedPieceTransform.parent.position; // Reset position
            return;
        }

        // Determine the destination square based on the name of the closest board square transform.
        Square endSquare = new Square(closestBoardSquareTransform.name);

        // Attempt to retrieve a legal move from the game logic.
        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            // If no legal move is found, reset the piece's position.
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        // If the move is a promotion move, set the promotion piece.
        if (move is PromotionMove promotionMove)
        {
            promotionMove.SetPromotionPiece(promotionPiece);
        }

        // Execute the move only if allowed.
        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove))
            && TryExecuteMove(move))
        {
            // For non-special moves, update the board visuals by destroying any piece at the destination.
            if (move is not SpecialMove) { BoardManager.Instance.TryDestroyVisualPiece(move.End); }

            // For promotion moves, update the moved piece transform to the newly created visual piece.
            if (move is PromotionMove)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            // Re-parent the moved piece to the destination square and update its position.
            movedPieceTransform.parent = closestBoardSquareTransform;
            movedPieceTransform.position = closestBoardSquareTransform.position;
        }
    }


    /// <summary>
    /// Determines whether the specified piece has any legal moves.
    /// </summary>
    /// <param name="piece">The chess piece to evaluate.</param>
    /// <returns>True if the piece has at least one legal move; otherwise, false.</returns>
    public bool HasLegalMoves(Piece piece)
    {
        return game.TryGetLegalMovesForPiece(piece, out _);
    }
}
