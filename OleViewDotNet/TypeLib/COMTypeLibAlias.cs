﻿//    This file is part of OleViewDotNet.
//    Copyright (C) James Forshaw 2014, 2016
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

using OleViewDotNet.TypeLib.Instance;
using OleViewDotNet.Utilities.Format;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;

namespace OleViewDotNet.TypeLib;

public sealed class COMTypeLibAlias : COMTypeLibTypeInfo
{
    public COMTypeLibTypeDesc AliasType { get; private set; }

    internal COMTypeLibAlias(COMTypeDocumentation doc, TYPEATTR attr, IEnumerable<COMTypeCustomDataItem> custom_data)
       : base(doc, attr, custom_data)
    {
    }

    private protected override void OnParse(COMTypeLibTypeInfoParser type_info, TYPEATTR attr)
    {
        AliasType = COMTypeLibTypeDesc.Parse(type_info, attr.tdescAlias);
    }

    internal override void FormatInternal(COMSourceCodeBuilder builder)
    {
        builder.AppendLine($"typedef {GetTypeAttributes("public").FormatAttrs()}{AliasType.FormatType()} {Name}{AliasType.FormatPostName()};");
    }
}
