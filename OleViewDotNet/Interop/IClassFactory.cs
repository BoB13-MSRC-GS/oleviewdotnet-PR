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
using System.Runtime.InteropServices;

namespace OleViewDotNet.Interop;

[ComImport, Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClassFactory
{
    void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
        in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);
    void LockServer(bool fLock);
}
