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

using System;
using System.Runtime.InteropServices.ComTypes;

namespace OleViewDotNet.TypeLib.Instance;

public sealed class COMTypeCompBindResultFunc : COMTypeCompBindResult
{
    private readonly IntPtr m_func_desc;
    public FUNCDESC FuncDesc { get; }

    internal COMTypeCompBindResultFunc(ITypeInfo type_info, IntPtr func_desc) 
        : base(type_info, DESCKIND.DESCKIND_FUNCDESC)
    {
        m_func_desc = func_desc;
        FuncDesc = func_desc.GetStructure<FUNCDESC>();
    }

    protected override void OnDispose()
    {
        TypeInfo.Instance.ReleaseFuncDesc(m_func_desc);
        base.OnDispose();
    }
}
