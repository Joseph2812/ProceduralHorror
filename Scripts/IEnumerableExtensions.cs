using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Generation;

namespace Scripts.Extensions;

public static class IEnumerableExtensions
{
    /// <summary>
    /// Gets a random element using each element's weightings.<para/>
    /// </summary>
    /// <typeparam name="T">Type containing a weight value.</typeparam>
    /// <param name="sequence">Sequence to pick from.</param>
    /// <param name="weightSelector">Where to get weight.</param>
    /// <returns></returns>
    public static T GetRandomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, int> weightSelector)
    {
        int totalWeight = sequence.Sum(weightSelector);

        // Weight index to sum up to
        int itemWeightIndex = MapGenerator.Inst.Rng.RandiRange(1, totalWeight); // 1 = Prevents 1st element with weight = 0 to be guaranteed
        int currentWeightIndex = 0;

        foreach (T item in sequence)
        {
            currentWeightIndex += weightSelector(item);
            if (currentWeightIndex >= itemWeightIndex) { return item; }
        }
        return default;
    }
}
