// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameState.vala, class GameScorePlayer (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class GameScorePlayer
{
    public GameScorePlayer(string name, int index, Wind wind, int startingPoints, int score, int transfer, int deposit = 0)
    {
        Name = name;
        Index = index;
        Wind = wind;
        Points = startingPoints;
        Score = score;
        Transfer = transfer;
        Deposit = deposit;
    }

    public string Name { get; private set; }
    public int Index { get; private set; }
    public Wind Wind { get; set; }
    public int Points { get; set; } // Regular game points
    public int Score { get; set; } // +- score
    public int Transfer { get; set; }

    // Not in the Vala: the riichi deposit the player put down this round (1000, or 0; given back if the riichi discard
    // is ronned). It leaves the player's points as it goes down (GameState.ShownPoints), so the round's own result,
    // Change, leaves it out (requested: a four-riichi draw showed -1,000 for everyone).
    public int Deposit { get; set; }
    public int Change => Transfer + Deposit;
}
