using System;
using System.Collections.Generic;

namespace ImmichFrame.Models;

/// <summary>
/// Keeps a bounded navigation history without retaining image data.
/// </summary>
public sealed class NavigationHistory<T> where T : class
{
    private readonly int capacity;
    private readonly List<T> items = new();
    private int currentIndex = -1;

    public NavigationHistory(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
    }

    public int Count => items.Count;
    public T? Current => currentIndex >= 0 ? items[currentIndex] : null;
    public bool CanMovePrevious => currentIndex > 0;
    public bool CanMoveNext => currentIndex >= 0 && currentIndex < items.Count - 1;

    public void Add(T item)
    {
        if (currentIndex < items.Count - 1)
            items.RemoveRange(currentIndex + 1, items.Count - currentIndex - 1);

        items.Add(item);
        currentIndex = items.Count - 1;

        if (items.Count > capacity)
        {
            items.RemoveAt(0);
            currentIndex--;
        }
    }

    public bool TryMovePrevious(out T? item)
    {
        if (!CanMovePrevious)
        {
            item = null;
            return false;
        }

        item = items[--currentIndex];
        return true;
    }

    public bool TryMoveNext(out T? item)
    {
        if (!CanMoveNext)
        {
            item = null;
            return false;
        }

        item = items[++currentIndex];
        return true;
    }
}
