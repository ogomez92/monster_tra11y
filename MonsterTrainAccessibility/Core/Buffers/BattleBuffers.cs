using System.Collections.Generic;
using MonsterTrainAccessibility.Battle;
using MonsterTrainAccessibility.Screens;

namespace MonsterTrainAccessibility.Core.Buffers
{
    /// <summary>
    /// Registers the battle review buffers (Hand, Floors, Units, Resources).
    /// Each provider returns null outside battle so the buffer is skipped
    /// when cycling with Ctrl+Left/Right.
    /// </summary>
    internal static class BattleBuffers
    {
        public static void Register(BufferManager buffers)
        {
            buffers.Register("Hand", () =>
            {
                var cache = GetBattleCache();
                if (cache == null)
                    return null;
                return HandReader.GetHandCardStrings(cache);
            });

            buffers.Register("Floors", () =>
            {
                var cache = GetBattleCache();
                if (cache == null)
                    return null;

                var items = new List<string>();
                for (int roomIndex = 0; roomIndex <= 3; roomIndex++)
                {
                    string summary = FloorReader.GetFloorSummary(cache, roomIndex, BattleAccessibility.AnnouncedKeywords);
                    if (!string.IsNullOrEmpty(summary))
                        items.Add($"{FloorReader.RoomIndexToFloorName(roomIndex)}: {summary}");
                }
                return items;
            });

            buffers.Register("Units", () =>
            {
                var cache = GetBattleCache();
                if (cache == null)
                    return null;
                return EnemyReader.GetUnitStrings(cache, BattleAccessibility.AnnouncedKeywords);
            });

            buffers.Register("Resources", () =>
            {
                var cache = GetBattleCache();
                if (cache == null)
                    return null;
                return ResourceReader.GetResourceStrings(cache);
            });
        }

        private static BattleManagerCache GetBattleCache()
        {
            var battle = MonsterTrainAccessibility.BattleHandler;
            if (battle == null || !battle.IsInBattle)
                return null;
            return battle.Cache;
        }
    }
}
