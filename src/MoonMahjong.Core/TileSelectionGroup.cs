// Ported from Practice-Riichi-Mahjong source/Game/Logic/ClientRoundState.vala, class TileSelectionGroup (OpenRiichi, GPLv3).
using System.Collections.Generic;

namespace MoonMahjong.Core;

// A choice offered to the human player: clicking any of SelectionTiles picks the group; HighlightTiles are the
// tiles to highlight for it (for a chii: the discard followed by the two hand tiles).
public sealed class TileSelectionGroup
{
    public TileSelectionGroup(List<Tile> selectionTiles, List<Tile> highlightTiles, GroupType groupType)
    {
        SelectionTiles = selectionTiles;
        HighlightTiles = highlightTiles;
        Type = groupType;
    }

    public List<Tile> SelectionTiles { get; private set; }
    public List<Tile> HighlightTiles { get; private set; }

    // Vala: group_type. The mechanical name GroupType would clash with the nested enum below (CS0102),
    // as with RoundStateCall.Type.
    public GroupType Type { get; private set; }

    public enum GroupType
    {
        Discard,
        Chii,
        ClosedKan,
        LateKan,
        Riichi,
    }
}
