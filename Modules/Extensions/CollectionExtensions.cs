﻿using System;

namespace TOHE;

// Credit: Endless Host Roles by Gurge44
// Reference: https://github.com/Gurge44/EndlessHostRoles/blob/main/Modules/Extensions/CollectionExtensions.cs
public static class CollectionExtensions
{
    /// <summary>
    /// Returns a random element from a collection
    /// </summary>
    /// <param name="collection">The collection</param>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <returns>A random element from the collection, or the default value of <typeparamref name="T"/> if the collection is empty</returns>
    public static T RandomElement<T>(this IList<T> collection)
    {
        if (collection.Count == 0) return default;
        return collection[IRandom.Instance.Next(collection.Count)];
    }
    /// <summary>
    /// Combines multiple collections into a single collection
    /// </summary>
    /// <param name="firstCollection">The collection to start with</param>
    /// <param name="collections">The other collections to add to <paramref name="firstCollection"/></param>
    /// <typeparam name="T">The type of the elements in the collections to combine</typeparam>
    /// <returns>A collection containing all elements of <paramref name="firstCollection"/> and all <paramref name="collections"/></returns>
    public static IEnumerable<T> CombineWith<T>(this IEnumerable<T> firstCollection, params IEnumerable<T>[] collections)
    {
        return firstCollection.Concat(collections.SelectMany(x => x));
    }

    /// <summary>
    /// Shuffles all elements in a collection randomly
    /// </summary>
    /// <typeparam name="T">The type of the collection</typeparam>
    /// <param name="collection">The collection to be shuffled</param>
    /// <param name="random">An instance of a randomizer algorithm</param>
    /// <returns>The shuffled collection</returns>
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> collection, IRandom random)
    {
        var list = collection.ToList();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
        return list;
    }

    /// <summary>
    /// Filters a IEnumerable(<typeparamref name="TDelegate"/>) of any duplicates
    /// </summary>
    /// <typeparam name="TDelegate">The type of the delegates in the collection</typeparam>
    /// <returns>A HashSet(<typeparamref name="TDelegate"/>) without duplicate object references nor static duplicates.</returns>
    public static HashSet<TDelegate> FilterDuplicates<TDelegate>(this IEnumerable<TDelegate> collection) where TDelegate : Delegate
    {
        // Filter out delegates which do not have a object reference (static methods)
        var filteredCollection = collection.Where(d => d.Target != null);

        // Group by the target object (the instance the method belongs to) and select distinct
        var distinctDelegates = filteredCollection
            .GroupBy(d => d.Target.GetType())
            .Select(g => g.First())
            .Concat(collection.Where(x => x.Target == null)); // adds back static methods

        return distinctDelegates.ToHashSet();
    }
    /// <summary>
    /// Return the first byte of a HashSet(Byte)
    /// </summary>
    public static byte First(this HashSet<byte> source)
        => source.ToArray().First();
}
