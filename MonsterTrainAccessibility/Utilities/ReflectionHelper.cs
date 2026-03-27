using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Shared reflection helpers for finding game types and manager instances at runtime.
    /// </summary>
    public static class ReflectionHelper
    {
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

        /// <summary>
        /// Find a game type by name, searching Assembly-CSharp and all loaded assemblies.
        /// Results are cached for performance.
        /// </summary>
        public static Type FindType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (_typeCache.TryGetValue(typeName, out var cached))
                return cached;

            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null) break;
                }
            }

            _typeCache[typeName] = type;
            return type;
        }

        /// <summary>
        /// Find a singleton manager instance using Unity's FindObjectOfType.
        /// </summary>
        public static object FindManager(string typeName)
        {
            try
            {
                var type = FindType(typeName);
                if (type == null) return null;

                // Use the non-generic FindObjectOfType with type parameter
                var findMethod = typeof(UnityEngine.Object).GetMethod(
                    "FindObjectOfType",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Type) },
                    null);

                if (findMethod != null)
                {
                    return findMethod.Invoke(null, new object[] { type });
                }

                // Fallback: try generic version via reflection
                var genericFind = typeof(UnityEngine.Object)
                    .GetMethod("FindObjectOfType", Type.EmptyTypes);
                if (genericFind != null)
                {
                    var specificFind = genericFind.MakeGenericMethod(type);
                    return specificFind.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"FindManager({typeName}): {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Convert a room index (0-3) to a user-facing floor number (1-3) or "Pyre".
        /// Room 0 = Floor 3 (Top), Room 1 = Floor 2, Room 2 = Floor 1, Room 3 = Pyre.
        /// </summary>
        public static int RoomIndexToUserFloor(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex > 3) return roomIndex;
            return 3 - roomIndex;
        }

        /// <summary>
        /// Get a friendly floor name from a room index.
        /// </summary>
        public static string RoomIndexToFloorName(int roomIndex)
        {
            if (roomIndex == 3) return "Pyre room";
            int floor = RoomIndexToUserFloor(roomIndex);
            if (floor >= 1 && floor <= 3) return $"Floor {floor}";
            return $"Room {roomIndex}";
        }
    }
}
