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

using NtApiDotNet.Ndr;
using OleViewDotNet.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OleViewDotNet.Utilities.Format;

public sealed class COMSourceCodeBuilder : NdrStringBuilder
{
    #region Private Members
    private readonly COMRegistry m_registry;
    private Tuple<COMSourceCodeBuilderType, bool> m_current_ndr_config = default;
    private INdrFormatter m_formatter;

    private struct StackPopper : IDisposable
    {
        private readonly COMSourceCodeBuilder _builder;

        public StackPopper(COMSourceCodeBuilder builder)
        {
            _builder = builder;
        }

        void IDisposable.Dispose()
        {
            _builder.PopIndent();
        }
    }
    #endregion

    #region Public Properties
    public COMSourceCodeBuilderType OutputType { get; set; }
    public bool HideComments { get; set; }
    public bool InterfacesOnly { get; set; }
    #endregion

    #region Constructors
    public COMSourceCodeBuilder(
        COMRegistry registry = null)
    {
        m_registry = registry;
    }
    #endregion

    #region Internal Methods
    internal INdrFormatter GetNdrFormatter()
    {
        Tuple<COMSourceCodeBuilderType, bool> config = Tuple.Create(OutputType, HideComments);
        if (config == m_current_ndr_config && m_formatter is not null)
        {
            return m_formatter;
        }
        m_current_ndr_config = config;

        DefaultNdrFormatterFlags flags = 0;

        if (HideComments)
        {
            flags |= DefaultNdrFormatterFlags.RemoveComments;
        }

        m_formatter = OutputType switch
        {
            COMSourceCodeBuilderType.Idl => IdlNdrFormatter.Create(m_registry?.IidNameCache, s => WinRTNameUtils.DemangleName(s), flags),
            COMSourceCodeBuilderType.Generic => DefaultNdrFormatter.Create(m_registry?.IidNameCache, s => WinRTNameUtils.DemangleName(s), flags),
            COMSourceCodeBuilderType.Cpp => CppNdrFormatter.Create(m_registry?.IidNameCache, s => WinRTNameUtils.DemangleName(s), flags),
            _ => throw new ArgumentException("Invalid output type."),
        };
        return m_formatter;
    }

    internal void AppendList(IEnumerable<string> lines)
    {
        var ls = lines.ToArray();
        if (ls.Length == 0)
            return;
        for (int i = 0; i < ls.Length - 1; ++i)
        {
            AppendLine($"{ls[i]},");
        }
        AppendLine(ls[ls.Length - 1]);
    }

    internal void AppendAttributes(IEnumerable<string> lines, int indent = 4)
    {
        if (!lines.Any())
            return;
        AppendLine("[");
        using (PushIndent(indent))
        {
            AppendList(lines);
        }
        AppendLine("]");
    }

    internal void AppendObjects(IEnumerable<ICOMSourceCodeFormattable> list)
    {
        foreach (var obj in list)
        {
            obj.Format(this);
            AppendLine();
        }
    }

    internal void AppendTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is not ICOMSourceCodeFormattable formattable)
            {
                formattable = new SourceCodeFormattableType(type);
            }
            formattable.Format(this);
            AppendLine();
        }
    }

    internal void AppendCommentLine(string comment)
    {
        if (HideComments)
            return;
        AppendLine(comment);
    }
    #endregion

    #region Public Methods
    public IDisposable PushIndent(int count)
    {
        PushIndent(' ', count);
        return new StackPopper(this);
    }

    public void AppendObject(object obj, bool parse = false)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        ICOMSourceCodeFormattable formattable = obj as ICOMSourceCodeFormattable;
        if (formattable is null)
        {
            if (obj is Type type)
            {
                formattable = new SourceCodeFormattableType(type);
            }
            else if (obj is Assembly asm)
            {
                formattable = new SourceCodeFormattableAssembly(asm, true);
            }
        }

        if (formattable is null || !formattable.IsFormattable)
        {
            throw new ArgumentException($"Object {obj} is not formattable.");
        }

        if (formattable is ICOMSourceCodeParsable parsable && !parsable.IsSourceCodeParsed)
        {
            if (!parse)
            {
                throw new ArgumentException($"Object {obj} must be parsed before formatting.");
            }

            parsable.ParseSourceCode();
        }

        formattable.Format(this);
    }
    #endregion
}
