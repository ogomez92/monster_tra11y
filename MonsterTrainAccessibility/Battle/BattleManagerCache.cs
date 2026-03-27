using System;
using System.Linq;
using System.Reflection;

namespace MonsterTrainAccessibility.Screens
{
    /// <summary>
    /// Holds cached references to game managers and MethodInfo objects
    /// discovered at runtime via reflection. Shared by all battle reader classes.
    /// </summary>
    public class BattleManagerCache
    {
        // Cached manager references (found at runtime)
        internal object CardManager;
        internal object SaveManager;
        internal object RoomManager;
        internal object PlayerManager;
        internal object CombatManager;

        // Cached reflection info
        internal MethodInfo GetHandMethod;
        internal MethodInfo GetTitleMethod;
        internal MethodInfo GetCostMethod;
        internal MethodInfo GetTowerHPMethod;
        internal MethodInfo GetMaxTowerHPMethod;
        internal MethodInfo GetEnergyMethod;
        internal MethodInfo GetGoldMethod;
        internal MethodInfo GetRoomMethod;
        internal MethodInfo GetSelectedRoomMethod;
        internal MethodInfo GetHPMethod;
        internal MethodInfo GetAttackDamageMethod;
        internal MethodInfo GetTeamTypeMethod;
        internal MethodInfo GetCharacterNameMethod;
        private bool _roomManagerMethodsLogged = false;

        /// <summary>
        /// Find and cache references to game managers
        /// </summary>
        public void FindManagers()
        {
            try
            {
                // Find managers using FindObjectOfType
                CardManager = FindManager("CardManager");
                SaveManager = FindManager("SaveManager");
                RoomManager = FindManager("RoomManager");
                PlayerManager = FindManager("PlayerManager");
                CombatManager = FindManager("CombatManager");

                // Cache method info for performance
                CacheMethodInfo();

                MonsterTrainAccessibility.LogInfo($"Found managers - CardManager: {CardManager != null}, SaveManager: {SaveManager != null}");
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding managers: {ex.Message}");
            }
        }

        internal object FindManager(string typeName)
        {
            try
            {
                // Find the type in the game assembly
                var type = Type.GetType(typeName + ", Assembly-CSharp");
                if (type == null)
                {
                    // Try without assembly qualifier
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(typeName);
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    // FindObjectOfType is a generic method, use reflection
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                    var genericMethod = findMethod.MakeGenericMethod(type);
                    return genericMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding {typeName}: {ex.Message}");
            }
            return null;
        }

        private void CacheMethodInfo()
        {
            try
            {
                if (CardManager != null)
                {
                    var cardManagerType = CardManager.GetType();
                    // GetHand has overloads, try to find the one without parameters or with bool
                    GetHandMethod = cardManagerType.GetMethod("GetHand", new Type[] { typeof(bool) })
                                  ?? cardManagerType.GetMethod("GetHand", Type.EmptyTypes);
                }

                if (SaveManager != null)
                {
                    var saveManagerType = SaveManager.GetType();
                    GetTowerHPMethod = saveManagerType.GetMethod("GetTowerHP", Type.EmptyTypes);
                    GetMaxTowerHPMethod = saveManagerType.GetMethod("GetMaxTowerHP", Type.EmptyTypes);
                    GetGoldMethod = saveManagerType.GetMethod("GetGold", Type.EmptyTypes);
                }

                if (PlayerManager != null)
                {
                    var playerManagerType = PlayerManager.GetType();
                    GetEnergyMethod = playerManagerType.GetMethod("GetEnergy", Type.EmptyTypes);
                }

                if (RoomManager != null)
                {
                    var roomManagerType = RoomManager.GetType();
                    // GetRoom takes an int parameter (room index)
                    GetRoomMethod = roomManagerType.GetMethod("GetRoom", new Type[] { typeof(int) });

                    // Log all RoomManager methods once to find the selected room method
                    if (!_roomManagerMethodsLogged)
                    {
                        _roomManagerMethodsLogged = true;
                        var methods = roomManagerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        var relevantMethods = methods.Where(m =>
                            m.Name.Contains("Room") || m.Name.Contains("Select") ||
                            m.Name.Contains("Active") || m.Name.Contains("Focus") ||
                            m.Name.Contains("Current") || m.Name.Contains("View") ||
                            m.Name.Contains("Index") || m.Name.Contains("Floor"))
                            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
                        MonsterTrainAccessibility.LogInfo($"RoomManager room-related methods: {string.Join(", ", relevantMethods)}");

                        // Also check properties
                        var properties = roomManagerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        var relevantProps = properties.Where(p =>
                            p.Name.Contains("Room") || p.Name.Contains("Select") ||
                            p.Name.Contains("Active") || p.Name.Contains("Focus") ||
                            p.Name.Contains("Current") || p.Name.Contains("View") ||
                            p.Name.Contains("Index") || p.Name.Contains("Floor"))
                            .Select(p => $"{p.Name} ({p.PropertyType.Name})");
                        MonsterTrainAccessibility.LogInfo($"RoomManager room-related properties: {string.Join(", ", relevantProps)}");
                    }

                    // Try to find the selected room method/property
                    GetSelectedRoomMethod = roomManagerType.GetMethod("GetSelectedRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetActiveRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetFocusedRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetCurrentRoom", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetSelectedRoomIndex", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetActiveRoomIndex", Type.EmptyTypes) ??
                                             roomManagerType.GetMethod("GetFocusedRoomIndex", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching manager methods: {ex.Message}");
            }

            // Cache game type methods separately to isolate errors
            try
            {
                // Cache CardState methods
                var cardStateType = Type.GetType("CardState, Assembly-CSharp");
                if (cardStateType != null)
                {
                    GetTitleMethod = cardStateType.GetMethod("GetTitle", Type.EmptyTypes);
                    GetCostMethod = cardStateType.GetMethod("GetCostWithoutTraits", Type.EmptyTypes)
                                  ?? cardStateType.GetMethod("GetCostWithoutAnyModifications", Type.EmptyTypes)
                                  ?? cardStateType.GetMethod("GetCost", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CardState methods: {ex.Message}");
            }

            try
            {
                // Cache CharacterState methods
                var characterStateType = Type.GetType("CharacterState, Assembly-CSharp");
                if (characterStateType != null)
                {
                    GetHPMethod = characterStateType.GetMethod("GetHP", Type.EmptyTypes);
                    GetAttackDamageMethod = characterStateType.GetMethod("GetAttackDamage", Type.EmptyTypes);
                    GetTeamTypeMethod = characterStateType.GetMethod("GetTeamType", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CharacterState methods: {ex.Message}");
            }

            try
            {
                // Cache CharacterData methods for getting name
                var characterDataType = Type.GetType("CharacterData, Assembly-CSharp");
                if (characterDataType != null)
                {
                    GetCharacterNameMethod = characterDataType.GetMethod("GetName", Type.EmptyTypes);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error caching CharacterData methods: {ex.Message}");
            }
        }
    }
}
