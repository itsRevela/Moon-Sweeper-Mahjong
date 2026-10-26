// Ported from Practice-Riichi-Mahjong source/Game/Logic/GameState.vala, class GameScorePlayer (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public sealed class GameScorePlayer
{
    public GameScorePlayer(string name, int index, Wind wind, int startingPoints, int score, int transfer)
    {
        Name = name;
        Index = index;
        Wind = wind;
        Points = startingPoints;
        Score = score;
        Transfer = transfer;
    }

    public string Name { get; private set; }
    public int Index { get; private set; }
    public Wind Wind { get; set; }
    public int Points { get; set; } // Regular game points
    public int Score { get; set; } // +- score
    public int Transfer { get; set; }
}
