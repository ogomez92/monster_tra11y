using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Builds meta progression readouts for the logbook from SaveManager game
    /// state rather than UI labels: covenant rank, best win streaks, challenge
    /// progress, per-clan levels/XP, champion unlocks, clan combo victories,
    /// and card collections. Used by the T key summary on the logbook
    /// Checklist and Statistics sections and by the clan row focus details.
    /// </summary>
    internal static class MetaProgressReader
    {
        private const int MAX_COVENANT = 25;

        /// <summary>
        /// Full summary of the current logbook section for the T key.
        /// Returns null when the open section has no tailored summary
        /// (callers fall back to the generic screen text dump).
        /// </summary>
        internal static string BuildCompendiumSummary()
        {
            try
            {
                var screen = ReflectionHelper.FindManager("CompendiumScreen");
                if (screen == null)
                    return null;

                string section = GetFieldValue(screen, "currentSection")?.ToString();
                switch (section)
                {
                    case "Checklist":
                        return BuildChecklistSummary();
                    case "Stats":
                        return BuildStatsSummary();
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building compendium summary: {ex.Message}");
            }
            return null;
        }

        // ===================== Checklist section =====================

        private static string BuildChecklistSummary()
        {
            var saveManager = ReflectionHelper.FindManager("SaveManager");
            if (saveManager == null)
                return null;

            bool dlcPage = IsHellforgedChecklistPageOpen();
            var lines = new List<string>();
            lines.Add(dlcPage ? "The Last Divinity progress." : "Progress summary.");

            var metagameSave = Invoke(saveManager, "GetMetagameSave");
            var balanceData = Invoke(saveManager, "GetBalanceData");
            if (metagameSave == null || balanceData == null)
                return null;

            // Covenant rank (max ascension level won, capped at 25 in the UI)
            int covenant = ToInt(Invoke(metagameSave, "GetMaxAscensionLevel", true));
            lines.Add($"Covenant Rank: {Math.Min(Math.Max(covenant, 0), MAX_COVENANT)} of {MAX_COVENANT}.");

            // Best win streaks per covenant bucket (the game hides these until
            // the first covenant victory)
            if (ToInt(Invoke(metagameSave, "GetMaxAscensionLevel", false)) > 0)
            {
                string streaks = BuildWinStreakLine(metagameSave);
                if (streaks != null)
                    lines.Add(streaks);
            }

            // Challenge trophies (only shown once the feature unlocks)
            string challenges = BuildChallengeLine(metagameSave, balanceData);
            if (challenges != null)
                lines.Add(challenges);

            // Per-clan progress
            var classDatas = Invoke(balanceData, "GetClassDatas") as IEnumerable;
            if (classDatas != null)
            {
                foreach (var classData in classDatas)
                {
                    if (classData == null) continue;
                    bool isDlcClan = GetRequiredDlcName(classData) != "None";
                    // Main page lists base clans; the Last Divinity page leads
                    // with the DLC clan's own progress
                    if (dlcPage != isDlcClan) continue;

                    string title = Invoke(classData, "GetTitle") as string ?? "Unknown clan";
                    var details = BuildClanDetails(classData, includeDlcContent: dlcPage);
                    lines.Add($"{title}: {string.Join(" ", details)}");
                }
            }

            // Clanless card collection
            string clanless = BuildCardCollectionLine(saveManager, null, dlcPage ? (string)null : "None", "Clanless cards");
            if (clanless != null)
                lines.Add(clanless);

            if (dlcPage)
            {
                AppendHellforgedExtras(saveManager, metagameSave, balanceData, lines);
            }

            return string.Join("\n", lines);
        }

        private static string BuildWinStreakLine(object metagameSave)
        {
            try
            {
                var bucketsField = metagameSave.GetType().GetField("WINSTREAK_BUCKET_START_LEVELS",
                    BindingFlags.Public | BindingFlags.Static);
                var buckets = bucketsField?.GetValue(null) as IEnumerable;
                if (buckets == null)
                    return null;

                var parts = new List<string>();
                foreach (var bucketObj in buckets)
                {
                    int bucket = ToInt(bucketObj);
                    var winStreak = Invoke(metagameSave, "GetBestWinStreakForLevel", bucket, true);
                    int length = winStreak != null ? ToInt(GetFieldValue(winStreak, "length")) : 0;

                    string name = LocalizationHelper.LocalizeOrNull($"WinStreak_BucketName{bucket}");
                    if (string.IsNullOrEmpty(name))
                        name = $"Covenant {bucket} and up";
                    parts.Add($"{name} {length}");
                }

                return parts.Count > 0 ? $"Best win streaks: {string.Join(", ", parts)}." : null;
            }
            catch { return null; }
        }

        private static string BuildChallengeLine(object metagameSave, object balanceData)
        {
            try
            {
                // MetagameSaveData.UnlockedFeature.SpChallenges gates the trophies row
                var featureEnum = metagameSave.GetType().GetNestedType("UnlockedFeature");
                if (featureEnum != null)
                {
                    object feature = Enum.Parse(featureEnum, "SpChallenges");
                    if (!(Invoke(metagameSave, "IsFeatureUnlocked", feature) is bool unlocked) || !unlocked)
                        return null;
                }

                var challenges = Invoke(balanceData, "GetSpChallenges") as IEnumerable;
                if (challenges == null)
                    return null;

                int total = 0;
                int completed = 0;
                var parts = new List<string>();
                foreach (var challenge in challenges)
                {
                    if (challenge == null) continue;
                    total++;
                    string id = Invoke(challenge, "GetID") as string;
                    string name = Invoke(challenge, "GetName") as string ?? "Unknown challenge";
                    bool divine = Invoke(metagameSave, "HasCompletedSpChallengeWithDivineVictory", id) is bool d && d;
                    bool done = divine || (Invoke(metagameSave, "HasCompletedSpChallenge", id) is bool c && c);
                    if (done) completed++;
                    parts.Add($"{name}: {(divine ? "divine victory" : done ? "completed" : "not completed")}");
                }

                if (total == 0)
                    return null;
                return $"Challenges completed: {completed} of {total}. {string.Join(". ", parts)}.";
            }
            catch { return null; }
        }

        /// <summary>
        /// Detail lines for one clan: level and XP (or unlock condition),
        /// champion unlocks, allied clan victories, and card collection.
        /// Also feeds the clan row focus readout in the logbook checklist.
        /// </summary>
        internal static List<string> BuildClanDetails(object classData, bool includeDlcContent)
        {
            var details = new List<string>();
            try
            {
                var saveManager = ReflectionHelper.FindManager("SaveManager");
                if (saveManager == null || classData == null)
                    return details;

                var metagameSave = Invoke(saveManager, "GetMetagameSave");
                var balanceData = Invoke(saveManager, "GetBalanceData");
                string classId = Invoke(classData, "GetID") as string;

                bool unlocked = InvokeOverload(saveManager, "IsUnlocked",
                    new[] { classData.GetType(), typeof(bool) }, classData, false) is bool u && u;

                if (!unlocked)
                {
                    string condition = LocalizationHelper.LocalizeOrNull(
                        Invoke(classData, "GetClassUnlockConditionKey") as string);
                    int progress = ToInt(Invoke(saveManager, "GetTrackedValue",
                        Invoke(classData, "GetClassUnlockCondition")));
                    int needed = ToInt(Invoke(classData, "GetClassUnlockParam"));
                    details.Add(string.IsNullOrEmpty(condition)
                        ? $"Locked. Progress {progress} of {needed}."
                        : $"Locked. {condition}: {progress} of {needed}.");
                    return details;
                }

                // Level and XP
                int level = ToInt(Invoke(saveManager, "GetClassLevel", classId));
                int maxLevel = ToInt(Invoke(balanceData, "GetMaximumClassLevel"));
                string levelLine = $"Level {level} of {maxLevel}.";
                if (Invoke(balanceData, "HasNextClassLevel", level) is bool hasNext && hasNext)
                {
                    int xp = ToInt(Invoke(saveManager, "GetClassXP", classId));
                    int xpNeeded = ToInt(Invoke(balanceData, "GetXPRequiredForNextClassLevel", level));
                    levelLine += $" {xp} of {xpNeeded} XP to the next level.";
                }
                details.Add(levelLine);

                // Champion unlocks
                string champions = BuildChampionLine(saveManager, classData);
                if (champions != null)
                    details.Add(champions);

                // Victories with each allied clan
                BuildVictoryLines(saveManager, metagameSave, balanceData, classData, classId,
                    includeDlcContent, details);

                // Card collection
                string cards = BuildCardCollectionLine(saveManager, classId,
                    includeDlcContent ? (string)null : "None", "Cards");
                if (cards != null)
                    details.Add(cards);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building clan details: {ex.Message}");
            }
            return details;
        }

        private static string BuildChampionLine(object saveManager, object classData)
        {
            try
            {
                var championsList = GetFieldValue(classData, "champions") as IList;
                if (championsList == null || championsList.Count == 0)
                    return null;

                var parts = new List<string>();
                for (int i = 0; i < championsList.Count; i++)
                {
                    var championData = championsList[i];
                    string name = null;
                    var cardData = championData != null ? GetFieldValue(championData, "championCardData") : null;
                    if (cardData != null)
                        name = Invoke(cardData, "GetName") as string;
                    if (string.IsNullOrEmpty(name))
                        name = $"Champion {i + 1}";

                    bool unlocked = InvokeOverload(saveManager, "IsUnlocked",
                        new[] { classData.GetType(), typeof(int) }, classData, i) is bool u && u;
                    parts.Add($"{name} {(unlocked ? "unlocked" : "locked")}");
                }

                return parts.Count > 0 ? $"Champions: {string.Join(", ", parts)}." : null;
            }
            catch { return null; }
        }

        private static void BuildVictoryLines(object saveManager, object metagameSave, object balanceData,
            object mainClassData, string mainClassId, bool includeDlcContent, List<string> details)
        {
            try
            {
                var classDatas = Invoke(balanceData, "GetClassDatas") as IEnumerable;
                if (classDatas == null)
                    return;

                int won = 0;
                var perClan = new List<string>();
                foreach (var subClassData in classDatas)
                {
                    if (subClassData == null) continue;
                    string subId = Invoke(subClassData, "GetID") as string;
                    if (subId == mainClassId) continue;
                    if (!includeDlcContent && GetRequiredDlcName(subClassData) != "None") continue;

                    string subTitle = Invoke(subClassData, "GetTitle") as string ?? "Unknown clan";
                    int bestCovenant = ToInt(InvokeOverload(metagameSave, "GetClassCombinationWinAscensionLevel",
                        new[] { typeof(string), typeof(string) }, mainClassId, subId));
                    if (bestCovenant >= 0)
                    {
                        won++;
                        perClan.Add($"with {subTitle}: best Covenant {Math.Min(bestCovenant, MAX_COVENANT)}");
                    }
                    else
                    {
                        perClan.Add($"with {subTitle}: not yet won");
                    }
                }

                if (perClan.Count == 0)
                    return;
                details.Add($"Victories: won with {won} of {perClan.Count} allied clans.");
                details.AddRange(perClan);
            }
            catch { }
        }

        /// <summary>
        /// "label: X of Y discovered, Z mastered." for a clan's (or the
        /// clanless) card collection. dlcFilterName: "None" restricts to base
        /// game cards, null includes everything.
        /// </summary>
        private static string BuildCardCollectionLine(object saveManager, string classId, string dlcFilterName, string label)
        {
            try
            {
                object dlcFilter = null;
                if (dlcFilterName != null)
                {
                    var dlcType = ReflectionHelper.FindType("DLC");
                    if (dlcType != null)
                        dlcFilter = Enum.Parse(dlcType, dlcFilterName);
                }

                var collection = Invoke(saveManager, "GetClassCardCollectionData", classId, dlcFilter);
                if (collection == null)
                    return null;

                int mastered = ToInt(GetFieldValue(collection, "masteredCards"));
                int discovered = ToInt(GetFieldValue(collection, "discoveredCards")) + mastered;
                int total = ToInt(GetFieldValue(collection, "totalCards"));
                if (total <= 0)
                    return null;

                return $"{label}: {discovered} of {total} discovered, {mastered} mastered.";
            }
            catch { return null; }
        }

        private static void AppendHellforgedExtras(object saveManager, object metagameSave, object balanceData,
            List<string> lines)
        {
            try
            {
                // The Last Divinity card collections for each base clan
                var classDatas = Invoke(balanceData, "GetClassDatas") as IEnumerable;
                if (classDatas != null)
                {
                    foreach (var classData in classDatas)
                    {
                        if (classData == null || GetRequiredDlcName(classData) != "None") continue;
                        string title = Invoke(classData, "GetTitle") as string ?? "Unknown clan";
                        string classId = Invoke(classData, "GetID") as string;
                        string line = BuildCardCollectionLine(saveManager, classId, "Hellforged",
                            $"{title} Last Divinity cards");
                        if (line != null)
                            lines.Add(line);
                    }
                }

                // Divine victories (defeating The Last Divinity boss per clan pair)
                var hellforgedSave = Invoke(metagameSave, "GetHellforgedMetagameSaveData");
                if (hellforgedSave != null && classDatas != null)
                {
                    int won = 0;
                    int total = 0;
                    foreach (var main in classDatas)
                    {
                        string mainId = Invoke(main, "GetID") as string;
                        foreach (var sub in classDatas)
                        {
                            string subId = Invoke(sub, "GetID") as string;
                            if (mainId == subId) continue;
                            total++;
                            int best = Math.Max(
                                ToInt(Invoke(hellforgedSave, "GetBossDefeatCovenantLevel", mainId, subId, 0)),
                                ToInt(Invoke(hellforgedSave, "GetBossDefeatCovenantLevel", mainId, subId, 1)));
                            if (best >= 0)
                                won++;
                        }
                    }
                    if (total > 0)
                        lines.Add($"Divine victories: The Last Divinity defeated with {won} of {total} clan combinations.");
                }
            }
            catch { }
        }

        private static bool IsHellforgedChecklistPageOpen()
        {
            try
            {
                var checklist = ReflectionHelper.FindManager("CompendiumSectionChecklist");
                if (checklist == null)
                    return false;
                var currentPage = GetFieldValue(checklist, "currentPage");
                var hellforgedPage = GetFieldValue(checklist, "hellforgedChecklistPage");
                return currentPage != null && ReferenceEquals(currentPage, hellforgedPage);
            }
            catch { return false; }
        }

        // ===================== Statistics section =====================

        private static string BuildStatsSummary()
        {
            var stats = ReflectionHelper.FindManager("CompendiumSectionStats");
            if (stats == null)
                return null;

            var currentPage = GetFieldValue(stats, "currentPage");
            var runStatsPage = GetFieldValue(stats, "runStatsPage");
            if (currentPage != null && ReferenceEquals(currentPage, runStatsPage))
                return BuildRunStatsSummary();
            return BuildLeaderboardSummary(GetFieldValue(stats, "statsLeaderboardPage"));
        }

        /// <summary>
        /// All populated leaderboard rows with the active stat type and page,
        /// marking the player's own row.
        /// </summary>
        private static string BuildLeaderboardSummary(object leaderboardPage)
        {
            try
            {
                if (leaderboardPage == null)
                    return null;

                var lines = new List<string>();

                // Active sort (Covenant Rank / Score / Wins / Win Streak)
                var sortOptions = GetFieldValue(leaderboardPage, "statsSortOptionsUI");
                string statType = null;
                if (sortOptions != null)
                {
                    var focus = GetPropertyValue(sortOptions, "PlayerStatFocus");
                    statType = StatTypeToName(GetPropertyValue(focus, "StatType")?.ToString());
                }
                lines.Add(statType != null
                    ? $"Stats Leaderboard, sorted by {statType}."
                    : "Stats Leaderboard.");

                // Player rows (unpopulated template rows have null playerStats)
                string playerId = Invoke(ReflectionHelper.FindManager("SaveManager"), "GetAnalyticsUserId") as string;
                var statRows = GetFieldValue(leaderboardPage, "statRows") as IEnumerable;
                int rowCount = 0;
                if (statRows != null)
                {
                    foreach (var row in statRows)
                    {
                        if (row == null) continue;
                        var playerStats = GetPropertyValue(row, "playerStats");
                        if (playerStats == null) continue;

                        string rank = GetPropertyValue(playerStats, "Rank")?.ToString();
                        string name = GetPropertyValue(playerStats, "PlayerFriendlyName") as string;
                        string rowPlayerId = GetPropertyValue(playerStats, "PlayerId") as string;
                        string value = GetTMPLabelText(row, "valueLabel");

                        var sb = new StringBuilder();
                        sb.Append($"Rank {rank}: {name}");
                        if (!string.IsNullOrEmpty(playerId) && rowPlayerId == playerId)
                            sb.Append(", you");
                        if (!string.IsNullOrEmpty(value))
                            sb.Append($", {value}");
                        lines.Add(sb.ToString());
                        rowCount++;
                    }
                }

                if (rowCount == 0)
                    lines.Add("No leaderboard entries loaded yet.");

                // Page position
                var pagination = GetFieldValue(leaderboardPage, "paginationControls");
                if (pagination != null)
                {
                    int page = ToInt(GetFieldValue(pagination, "currentPage"));
                    int last = ToInt(GetFieldValue(pagination, "lastPage"));
                    if (page >= 1 && last >= 1 && last != int.MaxValue)
                        lines.Add($"Page {page} of {last}.");
                }

                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building leaderboard summary: {ex.Message}");
                return null;
            }
        }

        internal static string StatTypeToName(string statType)
        {
            switch (statType)
            {
                case "AscensionLevel": return "Covenant Rank";
                case "Score": return "Score";
                case "WinCount": return "Wins";
                case "WinStreak": return "Win Streak";
                default: return statType;
            }
        }

        /// <summary>
        /// All visible rows on the Personal Records page (lifetime run stats).
        /// </summary>
        private static string BuildRunStatsSummary()
        {
            try
            {
                var rowType = ReflectionHelper.FindType("RunStatRow");
                if (rowType == null)
                    return null;

                var findMethod = typeof(UnityEngine.Object).GetMethod(
                    "FindObjectsOfType", new Type[] { typeof(Type) });
                var rows = findMethod?.Invoke(null, new object[] { rowType }) as UnityEngine.Object[];
                if (rows == null || rows.Length == 0)
                    return null;

                var lines = new List<string> { "Personal Records." };
                // FindObjectsOfType gives no guaranteed order; sort by hierarchy position
                var sorted = new List<Component>();
                foreach (var row in rows)
                {
                    if (row is Component c && c.gameObject.activeInHierarchy)
                        sorted.Add(c);
                }
                sorted.Sort((a, b) => CompareHierarchyOrder(a.transform, b.transform));

                foreach (var row in sorted)
                {
                    string name = GetTMPLabelText(row, "statNameLabel");
                    string value = GetTMPLabelText(row, "statValueLabel");
                    if (!string.IsNullOrEmpty(name))
                        lines.Add(string.IsNullOrEmpty(value) ? name : $"{name}: {value}");
                }

                return lines.Count > 1 ? string.Join("\n", lines) : null;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error building run stats summary: {ex.Message}");
                return null;
            }
        }

        private static int CompareHierarchyOrder(Transform a, Transform b)
        {
            // Compare sibling index paths from the root down
            var pathA = GetSiblingPath(a);
            var pathB = GetSiblingPath(b);
            for (int i = 0; i < Math.Min(pathA.Count, pathB.Count); i++)
            {
                if (pathA[i] != pathB[i])
                    return pathA[i].CompareTo(pathB[i]);
            }
            return pathA.Count.CompareTo(pathB.Count);
        }

        private static List<int> GetSiblingPath(Transform t)
        {
            var path = new List<int>();
            while (t != null)
            {
                path.Insert(0, t.GetSiblingIndex());
                t = t.parent;
            }
            return path;
        }

        // ===================== Reflection helpers =====================

        private static object Invoke(object target, string methodName, params object[] args)
        {
            if (target == null) return null;
            try
            {
                foreach (var method in target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name != methodName) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length != args.Length) continue;
                    return method.Invoke(target, args);
                }
            }
            catch { }
            return null;
        }

        private static object InvokeOverload(object target, string methodName, Type[] parameterTypes, params object[] args)
        {
            if (target == null) return null;
            try
            {
                var method = target.GetType().GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
                return method?.Invoke(target, args);
            }
            catch { }
            return null;
        }

        private static object GetFieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            try
            {
                var type = target.GetType();
                while (type != null)
                {
                    var field = type.GetField(fieldName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                        return field.GetValue(target);
                    type = type.BaseType;
                }
            }
            catch { }
            return null;
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null) return null;
            try
            {
                return target.GetType().GetProperty(propertyName,
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
            }
            catch { }
            return null;
        }

        private static string GetTMPLabelText(object target, string labelFieldName)
        {
            // Rendered text, not the .text property - the game sets these
            // labels via SetTextSafe/SetText, which leaves .text holding the
            // prefab placeholder (e.g. "12345")
            string text = UITextHelper.GetRenderedTMPLabelText(GetFieldValue(target, labelFieldName));
            return string.IsNullOrEmpty(text) ? null : TextUtilities.StripRichTextTags(text);
        }

        private static int ToInt(object value)
        {
            if (value is int i) return i;
            try { return value != null ? Convert.ToInt32(value) : 0; }
            catch { return 0; }
        }

        private static string GetRequiredDlcName(object classData)
        {
            return Invoke(classData, "GetRequiredDlc")?.ToString() ?? "None";
        }
    }
}
