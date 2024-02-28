﻿//    This file is part of OleViewDotNet.
//    Copyright (C) James Forshaw 2014
//
//    OleViewDotNet is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    OleViewDotNet is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with OleViewDotNet.  If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Text;

namespace OleViewDotNet.Utilities;

internal static class MiscUtilities
{
    public static string EscapeString(this string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        StringBuilder builder = new();
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                case '\f':
                    builder.Append(@"\f");
                    break;
                case '\v':
                    builder.Append(@"\v");
                    break;
                case '\b':
                    builder.Append(@"\b");
                    break;
                case '\0':
                    builder.Append(@"\0");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }
        return builder.ToString();
    }

    public static IEnumerable<T[]> Partition<T>(this IEnumerable<T> values, int partition_size)
    {
        List<T> list = new();
        foreach (var value in values)
        {
            list.Add(value);
            if (list.Count > partition_size)
            {
                yield return list.ToArray();
                list.Clear();
            }
        }
        if (list.Count > 0)
            yield return list.ToArray();
    }

    internal static bool EqualsDictionary<K, V>(IReadOnlyDictionary<K, V> left, IReadOnlyDictionary<K, V> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.ContainsKey(pair.Key))
            {
                return false;
            }

            if (!right[pair.Key].Equals(pair.Value))
            {
                return false;
            }
        }

        return true;
    }

    internal static int GetHashCodeDictionary<K, V>(IReadOnlyDictionary<K, V> dict)
    {
        int hash_code = 0;
        foreach (var pair in dict)
        {
            hash_code ^= pair.Key.GetHashCode() ^ pair.Value.GetHashCode();
        }
        return hash_code;
    }

    public static string GetFileName(string path)
    {
        int index = path.LastIndexOf('\\');
        if (index < 0)
        {
            index = path.LastIndexOf('/');
        }
        if (index < 0)
        {
            return path;
        }
        return path.Substring(index + 1);
    }
}