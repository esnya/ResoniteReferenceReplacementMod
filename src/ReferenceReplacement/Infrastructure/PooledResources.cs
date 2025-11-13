using System;
using System.Collections.Generic;
using Elements.Core;

namespace ReferenceReplacement.Infrastructure;

internal struct BorrowedList<T> : IDisposable
{
    private List<T>? _list;

    private BorrowedList(List<T> list)
    {
        _list = list;
    }

    public static BorrowedList<T> Rent(out List<T> list)
    {
        list = Pool.BorrowList<T>();
        return new BorrowedList<T>(list);
    }

    public void Dispose()
    {
        if (_list != null)
        {
            var list = _list;
            Pool.Return(ref list);
            _list = null;
        }
    }
}

internal struct BorrowedHashSet<T> : IDisposable
{
    private HashSet<T>? _set;

    private BorrowedHashSet(HashSet<T> set)
    {
        _set = set;
    }

    public static BorrowedHashSet<T> Rent(out HashSet<T> set)
    {
        set = Pool.BorrowHashSet<T>();
        return new BorrowedHashSet<T>(set);
    }

    public void Dispose()
    {
        if (_set != null)
        {
            var set = _set;
            Pool.Return(ref set);
            _set = null;
        }
    }
}
