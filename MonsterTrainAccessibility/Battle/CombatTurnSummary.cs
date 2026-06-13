using System.Collections.Generic;
using System.Linq;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Accumulates what happened during one turn's automated combat (the Combat and
    /// HeroTurn phases, plus any other non-MonsterTurn churn) so it can be announced as a
    /// single summary at the start of the player's next turn: who died on each floor and
    /// any pyre damage. Surviving units are read live from the board at summary time, so
    /// they are not tracked here.
    ///
    /// Owned by <see cref="BattleAccessibility"/>; reset once per turn (after the summary
    /// is spoken) and whenever a battle starts or ends.
    /// </summary>
    internal class CombatTurnSummary
    {
        private struct DeathRecord
        {
            public string Name;
            public bool IsEnemy;
            public int RoomIndex; // 0..2 floor, or -1/other = unknown
        }

        private readonly List<DeathRecord> _deaths = new List<DeathRecord>();

        private int _pyreDamageTotal;
        private int _pyreRemaining = -1;
        private bool _pyreTouched;

        public void Reset()
        {
            _deaths.Clear();
            _pyreDamageTotal = 0;
            _pyreRemaining = -1;
            _pyreTouched = false;
        }

        /// <summary>
        /// Record a unit death. <paramref name="isEnemy"/> = an enemy unit died.
        /// <paramref name="roomIndex"/> is the floor it died on (0..2), or -1/other if unknown.
        /// </summary>
        public void AddDeath(string unitName, bool isEnemy, int roomIndex)
        {
            if (string.IsNullOrEmpty(unitName)) return;
            _deaths.Add(new DeathRecord { Name = unitName, IsEnemy = isEnemy, RoomIndex = roomIndex });
        }

        public void AddPyre(int damage, int remaining)
        {
            if (damage <= 0) return;
            _pyreDamageTotal += damage;
            _pyreRemaining = remaining;
            _pyreTouched = true;
        }

        public List<string> EnemyDeathsOnFloor(int roomIndex)
            => _deaths.Where(d => d.IsEnemy && d.RoomIndex == roomIndex).Select(d => d.Name).ToList();

        public List<string> YourDeathsOnFloor(int roomIndex)
            => _deaths.Where(d => !d.IsEnemy && d.RoomIndex == roomIndex).Select(d => d.Name).ToList();

        // Deaths whose floor couldn't be determined (e.g. on the pyre, or cleared before capture).
        public List<string> EnemyDeathsOffFloor()
            => _deaths.Where(d => d.IsEnemy && (d.RoomIndex < 0 || d.RoomIndex > 2)).Select(d => d.Name).ToList();

        public List<string> YourDeathsOffFloor()
            => _deaths.Where(d => !d.IsEnemy && (d.RoomIndex < 0 || d.RoomIndex > 2)).Select(d => d.Name).ToList();

        public bool PyreTouched => _pyreTouched;
        public int PyreDamageTotal => _pyreDamageTotal;
        public int PyreRemaining => _pyreRemaining;
    }
}
