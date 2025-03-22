using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityChess;
using UnityEngine;
using Newtonsoft.Json;
using static UnityChess.SquareUtil;

/// <summary>
/// Manages the overall game state, including game start, moves execution,
/// special moves handling (such as castling, en passant, and promotion), and game reset.
/// Inherits from a singleton base class to ensure a single instance throughout the application.
/// </summary>
public class GameManager : NetworkBehaviourSingleton<GameManager>
{
    // Events signaling various game state changes.
    public static event Action NewGameStartedEvent;
    public static event Action GameEndedEvent;
    public static event Action GameResetToHalfMoveEvent;
    public static event Action MoveExecutedEvent;
    public NetworkVariable<Side> NetworkSideToMove = new NetworkVariable<Side>(Side.White,
    NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public ulong LocalPlayerId => NetworkManager.Singleton.LocalClientId;
    public List<ulong> PlayersConnected = new List<ulong>();

    public NetworkVariable<bool> isCurrTurn = new NetworkVariable<bool>();

    private Game game;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientPlayerConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientPlayerDisconnected;

        if (IsServer)
        {
            game = game ?? new Game(); // Ensure game is initialized
            NetworkSideToMove.Value = game.ConditionsTimeline[0].SideToMove;
        }
    }

    /// <summary>
    /// Returns the current board state.
    /// </summary>
    public Board CurrentBoard
    {
        get
        {
            game.BoardTimeline.TryGetCurrent(out Board currentBoard);
            return currentBoard;
        }
    }

    /// <summary>
    /// Returns the current turn's side.
    /// </summary>
    /*public Side SideToMove
    {
        get
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            return currentConditions.SideToMove;
        }
        set
        {
            game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
            currentConditions = new GameConditions(value,
                currentConditions.WhiteCanCastleKingside,
                currentConditions.WhiteCanCastleQueenside,
                currentConditions.BlackCanCastleKingside,
                currentConditions.BlackCanCastleQueenside,
                currentConditions.EnPassantSquare,
                currentConditions.HalfMoveClock,
                currentConditions.TurnNumber
            );
            game.ConditionsTimeline[game.ConditionsTimeline.HeadIndex] = currentConditions;
        }
    }*/
    public Side SideToMove
    {
        get => NetworkSideToMove.Value; // Clients now read from the NetworkVariable

        set
        {
            if (IsServer) // Only the server can modify this
            {
                game.ConditionsTimeline.TryGetCurrent(out GameConditions currentConditions);
                currentConditions = new GameConditions(value,
                    currentConditions.WhiteCanCastleKingside,
                    currentConditions.WhiteCanCastleQueenside,
                    currentConditions.BlackCanCastleKingside,
                    currentConditions.BlackCanCastleQueenside,
                    currentConditions.EnPassantSquare,
                    currentConditions.HalfMoveClock,
                    currentConditions.TurnNumber
                );
                game.ConditionsTimeline[game.ConditionsTimeline.HeadIndex] = currentConditions;

                // Sync with clients
                NetworkSideToMove.Value = value;
            }
            else
            {
                // If a client attempts to modify, request it from the server
                RequestSideToMoveServerRpc(value);
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSideToMoveServerRpc(Side requestedSide)
    {
        SideToMove = requestedSide; // Server updates the game state
    }

    public Side StartingSide => game.ConditionsTimeline[0].SideToMove;
    public Timeline<HalfMove> HalfMoveTimeline => game.HalfMoveTimeline;
    public int LatestHalfMoveIndex => game.HalfMoveTimeline.HeadIndex;
    public int FullMoveNumber => StartingSide switch
    {
        Side.White => LatestHalfMoveIndex / 2 + 1,
        Side.Black => (LatestHalfMoveIndex + 1) / 2 + 1,
        _ => -1
    };

    private bool isWhiteAI;
    private bool isBlackAI;

    public List<(Square, Piece)> CurrentPieces
    {
        get
        {
            List<(Square, Piece)> currentPieces = new List<(Square, Piece)>();
            for (int file = 1; file <= 8; file++)
            {
                for (int rank = 1; rank <= 8; rank++)
                {
                    Piece piece = CurrentBoard[file, rank];
                    if (piece != null)
                        currentPieces.Add((new Square(file, rank), piece));
                }
            }
            return currentPieces;
        }
    }

    // Reference to the debug utility for the chess engine.
    [SerializeField] private UnityChessDebug unityChessDebug;
    // The current game instance.
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

    public void Start()
    {
        VisualPiece.VisualPieceMoved += OnPieceMoved;
        StartNewGame();
    }

    public async void StartNewGame()
    {
        if (IsServer)
        {
            game = new Game();
            NewGameStartedEvent?.Invoke();
        }
    }

    /// <summary>
    /// Executes a move on the server and sends updates to clients.
    /// </summary>
    public void ExecuteMove(int fromFile, int fromRank, int toFile, int toRank)
    {
        Square startSquare = new Square(fromFile, fromRank);
        Square endSquare = new Square(toFile, toRank);

        if (!game.TryGetLegalMove(startSquare, endSquare, out Movement move))
        {
            Debug.LogWarning("[GameManager] Illegal move attempted!");
            return;
        }

        if (TryExecuteMove(move))
        {
            ApplyMoveClientRpc(JsonConvert.SerializeObject(move));
            AssignRole(); // <-- Ensure role assignment after move execution
        }
    }

    /// <summary>
    /// Sends move updates to all clients.
    /// </summary>
    [ClientRpc]
    private void ApplyMoveClientRpc(string moveJson)
    {
        Movement move = JsonConvert.DeserializeObject<Movement>(moveJson);
        BoardManager.Instance.MovePieceOnClient(move.Start.File, move.Start.Rank, move.End.File, move.End.Rank);
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
    /// Handles special move behavior asynchronously (castling, en passant, and promotion).
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
                UIManager.Instance.SetActivePromotionUI(true);
                BoardManager.Instance.SetActiveAllPieces(false);

                // Cancel any pending promotion UI tasks.
                promotionUITaskCancellationTokenSource?.Cancel();
                promotionUITaskCancellationTokenSource = new CancellationTokenSource();

                // Await user's promotion choice asynchronously.
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

            // Handle promotion move when the promotion piece is already set.
            case PromotionMove promotionMove:
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.Start);
                BoardManager.Instance.TryDestroyVisualPiece(promotionMove.End);
                BoardManager.Instance.CreateAndPlacePieceGO(promotionMove.PromotionPiece, promotionMove.End);
                return true;

            default:
                return false;
        }
    }

    private async void OnPieceMoved(Square movedPieceInitialSquare, Transform movedPieceTransform, Transform closestBoardSquareTransform, Piece promotionPiece = null)
    {
        ulong localPlayerId = NetworkManager.Singleton.LocalClientId;

        if (!IsPlayerTurn())
        {
            Debug.LogWarning($"[GameManager] Player {localPlayerId} tried moving but it's not their turn!");
            return;
        }

        Square endSquare = new Square(closestBoardSquareTransform.name);
        if (!game.TryGetLegalMove(movedPieceInitialSquare, endSquare, out Movement move))
        {
            Debug.LogWarning($"[GameManager] Illegal move attempted by Player {localPlayerId} from {movedPieceInitialSquare} to {endSquare}");
            movedPieceTransform.position = movedPieceTransform.parent.position;
            return;
        }

        if (move is PromotionMove promotionMove)
        {
            promotionMove.SetPromotionPiece(promotionPiece);
        }

        if ((move is not SpecialMove specialMove || await TryHandleSpecialMoveBehaviourAsync(specialMove)) && TryExecuteMove(move))
        {
            if (move is not SpecialMove)
            {
                BoardManager.Instance.TryDestroyVisualPiece(move.End);
            }

            if (move is PromotionMove)
            {
                movedPieceTransform = BoardManager.Instance.GetPieceGOAtPosition(move.End).transform;
            }

            int movePieceInitSquareFile = movedPieceInitialSquare.File;
            int movePieceInitSquareRank = movedPieceInitialSquare.Rank;
            int squareFile = endSquare.File;
            int squareRank = endSquare.Rank;

            // Request the server to move the piece
            RequestMovePieceServerRpc(movePieceInitSquareFile, movePieceInitSquareRank, squareFile, squareRank, movedPieceTransform.GetComponent<NetworkObject>());
        }
    }

    /// 🔹 **NEW**: Server RPC to process the move
    [ServerRpc(RequireOwnership = false)]
    private void RequestMovePieceServerRpc(int movePieceInitSquareFile, int movePieceInitSquareRank, int squareFile, int squareRank, NetworkObjectReference pieceRef)
    {
        Square movePieceInitSquare = new Square(movePieceInitSquareFile, movePieceInitSquareRank);
        Square endSquare = new Square(squareFile, squareRank);

        if (!game.TryGetLegalMove(movePieceInitSquare, endSquare, out Movement move))
        {
            Debug.LogWarning($"[GameManager] Server: Illegal move attempted from {movePieceInitSquare} to {endSquare}");
            return;
        }

        // ✅ Server updates game state
        if (TryExecuteMove(move))
        {
            if (move is not SpecialMove)
            {
                BoardManager.Instance.TryDestroyVisualPiece(move.End);
            }

            // ✅ Server instructs all clients to move the piece
            MovePieceOnClientsClientRpc(movePieceInitSquareFile, movePieceInitSquareRank, squareFile, squareRank, pieceRef);
        }
    }

    /// 🔹 **NEW**: Client RPC to update all players
    [ClientRpc]
    private void MovePieceOnClientsClientRpc(int movePieceInitSquareFile, int movePieceInitSquareRank, int squareFile, int squareRank, NetworkObjectReference pieceRef)
    {
        Square movePieceInitSquare = new Square(movePieceInitSquareFile, movePieceInitSquareRank);
        Square endSquare = new Square(squareFile, squareRank);

        if (pieceRef.TryGet(out NetworkObject pieceNetObj))
        {
            Transform pieceTransform = pieceNetObj.transform;
            GameObject targetSquareGO = BoardManager.Instance.GetSquareGOByPosition(endSquare);

            if (targetSquareGO != null)
            {
                pieceTransform.SetParent(targetSquareGO.transform);
                pieceTransform.position = targetSquareGO.transform.position;
                Debug.Log($"[BoardManager] Successfully moved piece on client to {endSquare}");
            }
            else
            {
                Debug.LogError($"[BoardManager] ERROR: Could not find target square {endSquare}");
            }
        }
    }

    public bool IsPlayerTurn()
    {
        if (PlayersConnected.Count != 2)
        {
            Debug.LogWarning($"[GameManager] Not all players are connected.");
            return false;
        }

        ulong localPlayerId = NetworkManager.Singleton.LocalClientId;
        Side turn = NetworkSideToMove.Value; // Ensure using NetworkVariable

        bool isTurn = (turn == Side.White && localPlayerId == PlayersConnected[0]) ||
                      (turn == Side.Black && localPlayerId == PlayersConnected[1]);

        Debug.Log($"[GameManager] Player {localPlayerId} turn check: {isTurn}, Turn: {turn}");
        return isTurn;
    }

    public void OnClientPlayerConnected(ulong clientId)
    {
        PlayersConnected.Add(clientId);
        UpdatePlayersClientRpc(PlayersConnected.ToArray());
    }

    private void OnClientPlayerDisconnected(ulong id)
    {
        PlayersConnected.Remove(id);
    }

    [ClientRpc]
    private void UpdatePlayersClientRpc(ulong[] playerIDs)
    {
        PlayersConnected = new List<ulong>(playerIDs);
    }

    public void AssignRole()
    {
        if (IsServer) // Ensure only the server changes the turn
        {
            Side nextTurn = NetworkSideToMove.Value == Side.White ? Side.Black : Side.White;
            NetworkSideToMove.Value = nextTurn; // Server sets the turn first

            Debug.Log($"[GameManager] Server updated turn to {nextTurn}");

            // Notify clients
            SyncTurnClientRpc(nextTurn);
        }
    }

    [ClientRpc]
    private void SyncTurnClientRpc(Side newTurn)
    {
        if (!IsServer)
        {
            Debug.Log($"[GameManager] Syncing turn: Now it's {newTurn}'s turn.");
            return; // Clients should NOT modify NetworkVariable
        }

        NetworkSideToMove.Value = newTurn; // Ensure only the SERVER modifies this
    }

    public string SerializeGame()
    {
        return JsonConvert.SerializeObject(game);
    }

    public void LoadGame(string serializedGame)
    {
        game = JsonConvert.DeserializeObject<Game>(serializedGame);
        NewGameStartedEvent?.Invoke();
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
    /// Allows the user to elect a promotion piece.
    /// </summary>
    /// <param name="choice">The elected promotion piece.</param>
    public void ElectPiece(ElectedPiece choice)
    {
        userPromotionChoice = choice;
    }

    public bool IsGameInitialized()
    {
        return game != null && game.BoardTimeline != null;
    }

    private bool TryExecuteMove(Movement move)
    {
        if (!game.TryExecuteMove(move))
        {
            return false;
        }

        HalfMoveTimeline.TryGetCurrent(out HalfMove latestHalfMove);

        if (latestHalfMove.CausedCheckmate || latestHalfMove.CausedStalemate)
        {
            BoardManager.Instance.SetActiveAllPieces(false);
            GameEndedEvent?.Invoke();
        }
        else
        {
            BoardManager.Instance.EnsureOnlyPiecesOfSideAreEnabled(SideToMove);
        }

        MoveExecutedEvent?.Invoke();
        return true;
    }
}
