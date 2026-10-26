// Ported from Practice-Riichi-Mahjong source/Game/Logic/TileRules.vala (OpenRiichi, GPLv3).
namespace MoonMahjong.Core;

public enum YakuType // Han
{
    // Yaku situations
    MenzenTsumo, // Closed tsumo
    Riichi, // Reach
    OpenRiichi, // Open reach
    Ippatsu, // One-shot
    DoubleRiichi, // Double reach
    HaiteiRaoyue, // Last tile tsumo
    HouteiRaoyui, // Last tile ron
    RinshanKaihou, // Out on extra tile
    Chankan, // Robbing a quad
    NagashiMangan, // Honorable discards

    // Yaku hands
    Pinfu, // No points
    Tanyao, // All simples
    Iipeikou, // Double sequence
    Yakuhai, // Special tiles
    SanshokuDoujun, // Three color sequence
    Ittsuu, // Straight
    Chanta, // Terminals or honours in each set
    Honroutou, // Terminals or honours
    Toitoi, // All triplets
    SanAnkou, // Three concealed triplets
    SanKantsu, // Three quads
    SanshokuDoukou, // Three colour triplets
    Chiitoi, // Seven pairs
    ShouSangen, // Little three dragons
    Honitsu, // Half flush
    Junchan, // Terminal in each set
    Ryanpeikou, // Two double sequences
    Chinitsu, // Full flush

    // Yakuman situations
    Tenhou, // Heavenly hand
    Chiihou, // Earthly hand
    Renhou, // Hand of man

    // Yakuman hands
    KokushiMusou, // Thirteen orphans
    DaiSangen, // Big three dragons (the Vala comment says "Big four winds", a copy-paste slip)
    ShouSuushii, // Little four winds
    DaiSuushii, // Big four winds
    ChuurenPoutou, // Nine gates
    SuuAnkou, // Four concealed triplets
    Ryuuiisou, // All green
    SuuKantsu, // Four kans
    TsuuIisou, // All honours
    Chinroutou, // All terminals

    // Dora
    Dora,
    UraDora,
    AkaDora,
}
