using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches
{
    /// <summary>
    /// Shared reflection helpers for reading game state from CharacterState, CardState, and RelicState.
    /// Used by new combat event patches to avoid duplicating reflection boilerplate.
    /// </summary>
    public static class CharacterStateHelper
    {
        public static string GetUnitName(object characterState)
        {
            if (characterState == null) return "Unit";
            try
            {
                var type = characterState.GetType();
                var getNameMethod = type.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(characterState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                var getDataMethod = type.GetMethod("GetCharacterDataRead") ?? type.GetMethod("GetCharacterData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(characterState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Unit";
        }

        public static bool IsEnemyUnit(object characterState)
        {
            if (characterState == null) return false;
            try
            {
                var getTeamMethod = characterState.GetType().GetMethod("GetTeamType");
                if (getTeamMethod != null)
                {
                    var team = getTeamMethod.Invoke(characterState, null);
                    return team?.ToString() == "Heroes";
                }
            }
            catch { }
            return false;
        }

        public static int GetCurrentHP(object characterState)
        {
            if (characterState == null) return -1;
            try
            {
                var getHPMethod = characterState.GetType().GetMethod("GetHP");
                if (getHPMethod != null)
                {
                    var result = getHPMethod.Invoke(characterState, null);
                    if (result is int hp) return hp;
                }
            }
            catch { }
            return -1;
        }

        public static int GetRoomIndex(object characterState)
        {
            if (characterState == null) return -1;
            try
            {
                var getRoomMethod = characterState.GetType().GetMethod("GetCurrentRoomIndex");
                if (getRoomMethod != null)
                {
                    var result = getRoomMethod.Invoke(characterState, null);
                    if (result is int index) return index;
                }
            }
            catch { }
            return -1;
        }

        public static string GetCardName(object cardState)
        {
            if (cardState == null) return "Card";
            try
            {
                // Try GetTitle first (CardState method)
                var getTitleMethod = cardState.GetType().GetMethod("GetTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var name = getTitleMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try GetName
                var getNameMethod = cardState.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(cardState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try through CardData
                var getDataMethod = cardState.GetType().GetMethod("GetCardDataRead");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(cardState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Card";
        }

        public static string GetRelicName(object relicState)
        {
            if (relicState == null) return "Artifact";
            try
            {
                var getNameMethod = relicState.GetType().GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(relicState, null) as string;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                        return name;
                }

                // Try through RelicData
                var getDataMethod = relicState.GetType().GetMethod("GetRelicDataRead") ??
                                     relicState.GetType().GetMethod("GetRelicData");
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(relicState, null);
                    if (data != null)
                    {
                        var dataGetNameMethod = data.GetType().GetMethod("GetName");
                        if (dataGetNameMethod != null)
                        {
                            var name = dataGetNameMethod.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(name) && !name.Contains("KEY>"))
                                return name;
                        }
                    }
                }
            }
            catch { }
            return "Artifact";
        }

        public static string CleanStatusName(string statusId)
        {
            if (string.IsNullOrEmpty(statusId))
                return "effect";

            string name = statusId
                .Replace("_StatusId", "")
                .Replace("StatusId", "")
                .Replace("_", " ");

            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name.ToLower().Trim();
        }
    }
}
