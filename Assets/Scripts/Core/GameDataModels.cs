using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#region Server Communication Models

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public Features features;
    public ServerUIData uiData;
    public ServerPlayer player;
    public JackpotData jackpotData;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;//jackpotfeature

}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

[Serializable]
public class ServerGameData
{
    public List<List<int>> lines;
    public List<double> bets;
    public double creditDivisor;
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public FreeGamesFeature freeGames;
    public int betMultiplier;
    public int maxWinMultiplier;
    public int minWinMultiplier;
}

[Serializable]
public class FreeGamesFeature
{
    public bool enabled;
    public double payMultiplier;
    public int maxTotalFreeGames;
}

[Serializable]
public class ExtraSpinsData
{
    [JsonProperty("2")] public int _2; // Keep for safety/compatibility with UI
    [JsonProperty("3")] public int _3;
    [JsonProperty("4")] public int _4;
    [JsonProperty("5")] public int _5;
}

[Serializable]
public class ServerUIData
{
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;
    public List<double> multiplier; // Keep for fallback compatibility
    public List<double> payout;
    public string description;
    public int minMatch;
}


[Serializable]
public class ServerPlayer
{
    public double balance;
}

// ============================================================================
// FIXED: Server Response Models - Must match actual server JSON structure
// ============================================================================

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    public List<List<string>> matrix; // Root level matrix sent by server
    public ServerPlayerBalance player;
    public ServerPayload payload;
}

[Serializable]
public class ServerPlayerBalance
{
    public double? balance; // Nullable because server sends null
}

[Serializable]
public class ServerPayload
{
    // Santa Riches result fields.
    public List<List<int>> matrix;
    public List<ServerPaylineWin> paylineWins;
    public List<int> expandedWilds;
    public List<ServerExtraGiftWild> extraGiftWilds;
    public double totalMultiplier;
    public ServerScatterResult scatter;
    public double baseLineWin;
    public bool isFreeSpin;

    // Older response fields retained only as parsing fallbacks.
    public List<List<string>> reels;        // Keep for fallback compatibility
    public double totalWin;                  // Keep for fallback compatibility
    public int scatterCount;
    public bool scatterTriggered;
    public bool isRoundOver;                 // True when free spin round is over
    public double totalRoundWin;             // Total round win (at payload level when isRoundOver)

    // Older result fields retained only for response compatibility.
    public double winAmount;
    public double grandTotalWin;
    public double netReturnRatio;
    public List<ServerWaysWin> waysWins;
    public ServerFreeGamesResult freeGames;
}

[Serializable]
public class ServerPaylineWin
{
    public int lineIndex;
    public List<int> lineDefinition;
    public List<ServerPosition> positions;
    public int symbolId;
    public int matchCount;
    public double basePayout;
    public double winInCredits;
}

[Serializable]
public class ServerExtraGiftWild
{
    public ServerPosition position;
    public int originalSymbolId;
}

[Serializable]
public class ServerScatterResult
{
    public bool triggered;
    public int scatterCount;
    public List<ServerPosition> positions;
    public bool freeGamesTriggered;
    public int spinsAwarded;
    public double payout;
}

[Serializable]
public class ServerWaysWin
{
    public int symbolId;
    public int matchCount;
    public int waysCount;
    public List<ServerPosition> matchedPositions;
    public double basePayout;
    public double appliedMultiplier;
    public double winInCredits;
    public double winInCash;
    public string winType;
}

[Serializable]
public class ServerPosition
{
    public int row;
    public int col;
}

[Serializable]
public class ServerFreeGamesResult
{
    public bool triggered;
    public int totalAwarded;
    public int played;
    public double totalFreeGamesWin;
}

// ============================================================================
// Client-Side Spin Request
// ============================================================================

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    public int betIndex;
    public bool isFreeSpin;
}

#region Santa Riches Initialization Schema

[Serializable]
public class ExpandingWild
{
    public bool enabled { get; set; }
    public List<int> baseGameReels { get; set; }
    public List<int> freeGameReels { get; set; }
    public int santaSymbolId { get; set; }
}

[Serializable]
public class ExtraGiftWilds
{
    public bool enabled { get; set; }
    public int maxThrow { get; set; }
    public int minThrow { get; set; }
    public double probability { get; set; }
    public int giftSymbolId { get; set; }
}

[Serializable]
public class Features
{
    public Scatter scatter { get; set; }
    public FreeGames freeGames { get; set; }
    public ExpandingWild expandingWild { get; set; }
    public ExtraGiftWilds extraGiftWilds { get; set; }
    public MultiplierWilds multiplierWilds { get; set; }
    public WildSubstitution wildSubstitution { get; set; }
}

[Serializable]
public class FreeGames
{
    public bool enabled { get; set; }
    public bool retrigger { get; set; }
    public int spinsAwarded { get; set; }
    public int triggerCount { get; set; }
    public int retriggerSpins { get; set; }
}

[Serializable]
public class GiftWildCountMultiplier
{
    [JsonProperty("1")]
    public int _1 { get; set; }

    [JsonProperty("2")]
    public int _2 { get; set; }
}

[Serializable]
public class Matrix
{
    public int x { get; set; }
    public int y { get; set; }
}

[Serializable]
public class MultiplierWilds
{
    public bool enabled { get; set; }
    public string stackMode { get; set; }
    public GiftWildCountMultiplier giftWildCountMultiplier { get; set; }
}

[Serializable]
public class Payouts
{
    [JsonProperty("3")]
    public int _3 { get; set; }

    [JsonProperty("4")]
    public int _4 { get; set; }

    [JsonProperty("5")]
    public int _5 { get; set; }
}

[Serializable]
public class ReelsInstance
{
    [JsonProperty("0")]
    public int _0 { get; set; }

    [JsonProperty("1")]
    public int _1 { get; set; }

    [JsonProperty("2")]
    public int _2 { get; set; }

    [JsonProperty("3")]
    public int _3 { get; set; }

    [JsonProperty("4")]
    public int _4 { get; set; }
}

[Serializable]
public class Root
{
    public string id { get; set; }
    public List<double> bets { get; set; }
    public string name { get; set; }
    public List<List<int>> lines { get; set; }
    public Matrix matrix { get; set; }
    public List<Symbol> symbols { get; set; }
    public Features features { get; set; }
    public int activeLine { get; set; }
}

[Serializable]
public class Scatter
{
    public bool enabled { get; set; }
    public int minTriggerCount { get; set; }
    public int scatterSymbolId { get; set; }
}

[Serializable]
public class Symbol
{
    public int id { get; set; }
    public string name { get; set; }
    public string group { get; set; }
    public Payouts payouts { get; set; }
    public string description { get; set; }
    public ReelsInstance reelsInstance { get; set; }
}

[Serializable]
public class WildSubstitution
{
    public bool enabled { get; set; }
    public List<int> wildSymbolIds { get; set; }
    public List<int> substituteAllExcept { get; set; }
}

#endregion




#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    public int rowCount = 3;
    public int symbolCount = 13;
    public int paylineCount = 243;
    public List<List<int>> paylines;
    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    // Wild configuration
    public int wildSymbolId;
    public int expandingWildSymbolId;
    public int giftWildSymbolId = 1;
    public List<int> wildSymbolIds = new List<int>();

    // Scatter configuration
    public int scatterSymbolId = 11;

    public int betMultiplier = 1;
    public double creditDivisor = 25;  // Credit divisor sent in initData
    public int maxWinMultiplier = 10000;
    public int minWinMultiplier = 10;
    public int initialFreeSpins = 12;
    public ExtraSpinsData extraSpinsData; // Keep to avoid compilation error in UI

}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    public List<double> multipliers;
    public bool isWild;
    public bool isScatter;
    public int wildMultiplier = 1;
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

/// <summary>
/// Runtime-only game data populated from server initialization, spin results,
/// balance synchronization and player bet selection. This type is deliberately
/// not serializable so authoritative values can never be edited in a Unity
/// Inspector.
/// </summary>
public sealed class GameRuntimeData
{
    public bool IsInitialized { get; private set; }
    public bool InitializationFailed { get; private set; }
    public PlayerData Player { get; private set; } = new PlayerData();
    public GameConfig Config { get; private set; } = new GameConfig();
    public int CurrentBetIndex { get; private set; }
    public double CurrentBetAmount { get; private set; }
    public double DisplayedBalance { get; private set; }

    public bool ApplyInitialization(GameConfig config, PlayerData player)
    {
        bool valid = config != null && player != null &&
            config.availableBets != null && config.availableBets.Count > 0;

        Config = config ?? new GameConfig();
        Player = player ?? new PlayerData();
        IsInitialized = valid;
        InitializationFailed = !valid;

        if (!valid)
        {
            CurrentBetIndex = 0;
            CurrentBetAmount = 0d;
            DisplayedBalance = Math.Max(0d, Player.balance);
            return false;
        }

        CurrentBetIndex = Math.Max(0, Math.Min(Player.currentBetIndex, Config.availableBets.Count - 1));
        Player.currentBetIndex = CurrentBetIndex;
        CurrentBetAmount = Config.availableBets[CurrentBetIndex];
        DisplayedBalance = Math.Max(0d, Player.balance);
        return true;
    }

    public void MarkInitializationFailed()
    {
        IsInitialized = false;
        InitializationFailed = true;
    }

    public bool SelectBet(int betIndex)
    {
        if (Config?.availableBets == null || Config.availableBets.Count == 0)
        {
            return false;
        }

        CurrentBetIndex = Math.Max(0, Math.Min(betIndex, Config.availableBets.Count - 1));
        CurrentBetAmount = Config.availableBets[CurrentBetIndex];
        Player.currentBetIndex = CurrentBetIndex;
        return true;
    }

    public void ShowOptimisticBalance(double totalBet)
    {
        DisplayedBalance = Math.Max(0d, Player.balance - Math.Max(0d, totalBet));
    }

    public void ApplySpinResult(SpinResult result)
    {
        if (result?.playerData == null)
        {
            return;
        }

        Player = result.playerData;
        Player.currentBetIndex = CurrentBetIndex;
        DisplayedBalance = Math.Max(0d, Player.balance);
    }

    public void SynchronizeBalance(double balance, bool updateDisplayedBalance)
    {
        Player.balance = Math.Max(0d, balance);
        if (updateDisplayedBalance)
        {
            DisplayedBalance = Player.balance;
        }
    }

    public void RestoreAuthoritativeBalance()
    {
        DisplayedBalance = Math.Max(0d, Player.balance);
    }
}

[Serializable]
public class SpinResult
{
    public List<List<int>> resultMatrix;  // Client uses int matrix
    public double winAmount;
    public double grandTotalWin;
    public List<WinLine> winLines;
    public PlayerData playerData;
    public FreeSpinData freeSpinData;
    public ScatterData scatterData;
    public OverlayScatterData overlayScatterData; // Keep for safety/UI compilation
    public Dictionary<string, int> stickyWilds;  // Keep for safety/UI compilation

    // Server-authoritative free spin state
    public int serverSpinsRemaining;
    public int serverSpinsUsed;
    public int serverTotalSpins;
    public double serverTotalRoundWin;
    public bool isRoundOver;
    public bool isFreeSpinResult;
    public List<int> expandedWildReels;
    public List<ServerExtraGiftWild> extraGiftWilds;
    public double totalMultiplier;
}

[Serializable]
public class WinLine
{
    public int lineId;
    public int symbolId;
    public List<int> positions;  // Flat list: [row * 5 + col]
    public double winAmount;
}

[Serializable]
public class FreeSpinData
{
    public bool isTriggered;
    public int spinsAwarded;
    public int remainingSpins;
    public bool isBought;
}

[Serializable]
public class ScatterData
{
    public bool isTriggered;
    public int scatterCount;
    public double winAmount;
    public List<int> positions;
}

[Serializable]
public class OverlayScatterData
{
    public bool isTriggered;
    public int count;
    public int extraSpins;
    public List<List<int>> positions;
}

#endregion

#region Platform Communication

[Serializable]
public class AuthData
{
    public string token;
    public string socketURL;
    public string nameSpace;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    ShowingWin,
    FreeSpinMode
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    RegularWin,         // Normal credit win (multiplier < 500x)
    BigWin,             // Big win (multiplier >= 500x)
    FreeSpinTrigger,
    FreeSpinComplete    // All free spins completed
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// Converts server data to client GameConfig
/// </summary>
public static class InitDataConverter
{
    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        var config = new GameConfig
        {
            reelCount = 5,
            rowCount = (serverData.gameData.totalLines == 243) ? 3 : (serverData.gameData.totalLines == 1024 ? 4 : 3),
            symbolCount = serverData.uiData.paylines.symbols.Count,
            paylineCount = serverData.gameData.totalLines,
            paylines = serverData.gameData.lines,
            availableBets = serverData.gameData.bets,
            creditDivisor = serverData.gameData.creditDivisor > 0
                ? serverData.gameData.creditDivisor
                : Math.Max(1, serverData.gameData.totalLines),
            symbols = new List<SymbolInfo>()
        };

        foreach (var serverSymbol in serverData.uiData.paylines.symbols)
        {
            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = serverSymbol.name,
                multipliers = new List<double>(),
                isWild = !string.IsNullOrEmpty(serverSymbol.name) && serverSymbol.name.ToLowerInvariant().Contains("wild"),
                isScatter = !string.IsNullOrEmpty(serverSymbol.name) && serverSymbol.name.ToLowerInvariant().Contains("scatter"),
                minMatch = serverSymbol.minMatch
            };

            // Store raw payout values for info page
            if (serverSymbol.payout != null)
            {
                for (int i = serverSymbol.payout.Count - 1; i >= 0; i--)
                {
                    symbolInfo.multipliers.Add(serverSymbol.payout[i]);
                }
            }
            config.symbols.Add(symbolInfo);

            if (symbolInfo.isWild)
            {
                config.wildSymbolId = symbolInfo.id;
            }
            if (symbolInfo.isScatter)
            {
                config.scatterSymbolId = symbolInfo.id;
            }
        }

        if (serverData.features != null)
        {
            if (serverData.features.freeGames != null)
            {
                config.initialFreeSpins = serverData.features.freeGames.spinsAwarded;
            }

            if (serverData.features.scatter != null)
            {
                config.scatterSymbolId = serverData.features.scatter.scatterSymbolId;
            }

            if (serverData.features.expandingWild != null)
            {
                config.expandingWildSymbolId = serverData.features.expandingWild.santaSymbolId;
            }

            if (serverData.features.extraGiftWilds != null)
            {
                config.giftWildSymbolId = serverData.features.extraGiftWilds.giftSymbolId;
            }

            if (serverData.features.wildSubstitution?.wildSymbolIds != null)
            {
                config.wildSymbolIds = new List<int>(serverData.features.wildSubstitution.wildSymbolIds);
            }
        }

        if (config.wildSymbolIds.Count == 0)
        {
            config.wildSymbolIds.Add(config.expandingWildSymbolId);
            if (!config.wildSymbolIds.Contains(config.giftWildSymbolId))
            {
                config.wildSymbolIds.Add(config.giftWildSymbolId);
            }
        }

        config.wildSymbolId = config.wildSymbolIds[0];
        foreach (SymbolInfo symbol in config.symbols)
        {
            symbol.isWild = config.wildSymbolIds.Contains(symbol.id);
            symbol.isScatter = symbol.id == config.scatterSymbolId;
        }

        return config;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer.balance,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts server response to client SpinResult
    /// </summary>
    internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
    {
        if (serverResponse?.payload == null)
        {
            throw new ArgumentException("The spin response payload is missing.", nameof(serverResponse));
        }

        double winAmountVal = serverResponse.payload.winAmount > 0 ? serverResponse.payload.winAmount : serverResponse.payload.totalWin;
        double totalPay = serverResponse.payload.isFreeSpin
            ? 0d
            : (gameConfig != null && gameConfig.creditDivisor > 0)
                ? betAmount * gameConfig.creditDivisor
                : betAmount * 25;
        double newBalance = serverResponse.player?.balance ?? CalculateNewBalance(currentBalance, totalPay, winAmountVal);

        int spinsRemaining = 0;
        int spinsUsed = 0;
        int totalSpins = 0;
        double totalRoundWin = 0;
        bool isRoundOver = false;

        bool santaFreeGamesTriggered = serverResponse.payload.scatter != null &&
                                       serverResponse.payload.scatter.freeGamesTriggered;

        if (serverResponse.payload.freeGames != null)
        {
            spinsRemaining = serverResponse.payload.freeGames.totalAwarded - serverResponse.payload.freeGames.played;
            spinsUsed = serverResponse.payload.freeGames.played;
            totalSpins = serverResponse.payload.freeGames.totalAwarded;
            totalRoundWin = serverResponse.payload.freeGames.totalFreeGamesWin;
            isRoundOver = serverResponse.payload.freeGames.played >= serverResponse.payload.freeGames.totalAwarded && serverResponse.payload.freeGames.totalAwarded > 0;
        }
        else
        {
            isRoundOver = serverResponse.payload.isRoundOver;
            totalRoundWin = serverResponse.payload.totalRoundWin;
        }

        double grandTotalWinVal = serverResponse.payload.grandTotalWin > 0 
            ? serverResponse.payload.grandTotalWin 
            : winAmountVal;

        var result = new SpinResult
        {
            resultMatrix = ConvertReelsToMatrix(
                serverResponse.payload.matrix,
                serverResponse.matrix,
                serverResponse.payload.reels,
                gameConfig),
            winAmount = winAmountVal,
            grandTotalWin = grandTotalWinVal,
            winLines = ConvertWinningLines(
                serverResponse.payload.paylineWins,
                serverResponse.payload.waysWins,
                gameConfig),

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeSpinData = santaFreeGamesTriggered
                ? new FreeSpinData
                {
                    isTriggered = true,
                    spinsAwarded = Math.Max(0, serverResponse.payload.scatter.spinsAwarded),
                    remainingSpins = Math.Max(0, serverResponse.payload.scatter.spinsAwarded),
                    isBought = false
                }
                : (serverResponse.payload.freeGames != null && serverResponse.payload.freeGames.triggered)
                ? new FreeSpinData
                {
                    isTriggered = true,
                    spinsAwarded = serverResponse.payload.freeGames.totalAwarded,
                    remainingSpins = serverResponse.payload.freeGames.totalAwarded - serverResponse.payload.freeGames.played,
                    isBought = false
                }
                : null,

            scatterData = serverResponse.payload.scatter != null
                ? new ScatterData
                {
                    isTriggered = serverResponse.payload.scatter.triggered,
                    scatterCount = serverResponse.payload.scatter.scatterCount,
                    winAmount = serverResponse.payload.scatter.payout,
                    positions = FlattenPositions(serverResponse.payload.scatter.positions, gameConfig)
                }
                : serverResponse.payload.scatterTriggered
                ? new ScatterData
                {
                    isTriggered = true,
                    scatterCount = serverResponse.payload.scatterCount,
                    winAmount = 0,
                    positions = new List<int>()
                }
                : null,

            overlayScatterData = null,
            stickyWilds = null,

            serverSpinsRemaining = spinsRemaining,
            serverSpinsUsed = spinsUsed,
            serverTotalSpins = totalSpins,
            serverTotalRoundWin = totalRoundWin,
            isRoundOver = isRoundOver,
            isFreeSpinResult = serverResponse.payload.isFreeSpin,
            expandedWildReels = serverResponse.payload.expandedWilds != null
                ? new List<int>(serverResponse.payload.expandedWilds)
                : new List<int>(),
            extraGiftWilds = serverResponse.payload.extraGiftWilds != null
                ? new List<ServerExtraGiftWild>(serverResponse.payload.extraGiftWilds)
                : new List<ServerExtraGiftWild>(),
            totalMultiplier = serverResponse.payload.totalMultiplier
        };

        return result;
    }


    private static List<List<int>> ConvertReelsToMatrix(
        List<List<int>> santaMatrix,
        List<List<string>> serverMatrix,
        List<List<string>> serverReels,
        GameConfig gameConfig)
    {
        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : 5;
        int rowCount = gameConfig != null && gameConfig.rowCount > 0 ? gameConfig.rowCount : 3;

        if (santaMatrix != null && santaMatrix.Count > 0)
        {
            return NormalizeMatrix(santaMatrix, reelCount, rowCount);
        }

        List<List<string>> stringMatrix = serverMatrix != null && serverMatrix.Count > 0
            ? serverMatrix
            : serverReels;

        if (stringMatrix == null || stringMatrix.Count == 0)
        {
            UnityEngine.Debug.LogError(
                "Invalid server matrix: payload.matrix, root matrix, and payload.reels are all null or empty.");
            return GenerateDefaultMatrix(reelCount, rowCount);
        }

        List<List<int>> parsedMatrix = new List<List<int>>();
        for (int row = 0; row < stringMatrix.Count; row++)
        {
            if (stringMatrix[row] == null)
            {
                UnityEngine.Debug.LogError($"Invalid server data at row {row}: row is null");
                return GenerateDefaultMatrix(reelCount, rowCount);
            }

            List<int> parsedRow = new List<int>();
            for (int col = 0; col < stringMatrix[row].Count; col++)
            {
                string symbolStr = stringMatrix[row][col];
                if (!int.TryParse(symbolStr, out int symbolId))
                {
                    UnityEngine.Debug.LogError($"Failed to parse symbol: {symbolStr}");
                    return GenerateDefaultMatrix(reelCount, rowCount);
                }

                parsedRow.Add(symbolId);
            }

            parsedMatrix.Add(parsedRow);
        }

        return NormalizeMatrix(parsedMatrix, reelCount, rowCount);
    }

    private static List<List<int>> NormalizeMatrix(List<List<int>> source, int reelCount, int rowCount)
    {
        bool isRowMajor = source.Count == rowCount && source.All(row => row != null && row.Count == reelCount);
        if (isRowMajor)
        {
            List<List<int>> reelMajorMatrix = new List<List<int>>(reelCount);
            for (int reel = 0; reel < reelCount; reel++)
            {
                List<int> column = new List<int>(rowCount);
                for (int row = 0; row < rowCount; row++)
                {
                    column.Add(source[row][reel]);
                }

                reelMajorMatrix.Add(column);
            }

            return reelMajorMatrix;
        }

        bool isReelMajor = source.Count == reelCount && source.All(column => column != null && column.Count == rowCount);
        if (isReelMajor)
        {
            return source.Select(column => new List<int>(column)).ToList();
        }

        UnityEngine.Debug.LogError(
            $"Invalid server matrix dimensions. Expected {rowCount}x{reelCount} rows or {reelCount}x{rowCount} reels.");
        return GenerateDefaultMatrix(reelCount, rowCount);
    }

    private static List<List<int>> GenerateDefaultMatrix(int reelCount, int rowCount)
    {
        List<List<int>> matrix = new List<List<int>>();
        for (int col = 0; col < reelCount; col++)
        {
            matrix.Add(Enumerable.Repeat(0, rowCount).ToList());
        }

        return matrix;
    }

    private static List<WinLine> ConvertWinningLines(
        List<ServerPaylineWin> paylineWins,
        List<ServerWaysWin> serverWaysWins,
        GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();

        if (paylineWins != null)
        {
            foreach (ServerPaylineWin paylineWin in paylineWins)
            {
                winLines.Add(new WinLine
                {
                    lineId = paylineWin.lineIndex,
                    symbolId = paylineWin.symbolId,
                    positions = FlattenPositions(
                        paylineWin.positions != null
                            ? paylineWin.positions.Take(Math.Max(0, paylineWin.matchCount)).ToList()
                            : null,
                        gameConfig),
                    winAmount = paylineWin.winInCredits
                });
            }

            return winLines;
        }

        if (serverWaysWins == null) return winLines;

        int index = 0;
        foreach (var waysWin in serverWaysWins)
        {
            winLines.Add(new WinLine
            {
                lineId = index++,
                symbolId = waysWin.symbolId,
                positions = FlattenPositions(waysWin.matchedPositions, gameConfig),
                winAmount = waysWin.winInCash > 0d ? waysWin.winInCash : waysWin.winInCredits
            });
        }

        return winLines;
    }

    private static List<int> FlattenPositions(List<ServerPosition> positions, GameConfig gameConfig)
    {
        List<int> flatPositions = new List<int>();
        if (positions == null)
        {
            return flatPositions;
        }

        int reelCount = gameConfig != null && gameConfig.reelCount > 0 ? gameConfig.reelCount : 5;
        foreach (ServerPosition position in positions)
        {
            if (position != null)
            {
                flatPositions.Add(position.row * reelCount + position.col);
            }
        }

        return flatPositions;
    }

    private static double CalculateNewBalance(double currentBalance, double totalPay, double winAmount)
    {
        return Math.Max(0d, currentBalance - totalPay) + winAmount;
    }
}

#endregion
