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

using System;
using System.Runtime.InteropServices;

namespace OleViewDotNet.Interop;

public class SafeComObjectHandle : SafeHandle
{
    internal SafeComObjectHandle() : base(IntPtr.Zero, true)
    {
    }

    internal SafeComObjectHandle(IntPtr handle) :this()
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        Marshal.Release(handle);
        return true;
    }

    public SafeComObjectHandle QueryInterface(Guid iid, bool throw_on_error = true)
    {
        if (IsClosed || IsInvalid)
        {
            throw new ObjectDisposedException(nameof(handle));
        }

        int hr = Marshal.QueryInterface(handle, ref iid, out IntPtr ppv);
        if (hr != 0)
        {
            if (throw_on_error)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return null;
        }
        return new SafeComObjectHandle(ppv);
    }

    public bool SupportsInterface(Guid iid)
    {
        using var obj = QueryInterface(iid, false);
        return obj != null;
    }

    public bool IsProxy()
    {
        return SupportsInterface(COMKnownGuids.IID_IProxyManager);
    }

    public IntPtr ReadVTable()
    {
        if (IsClosed || IsInvalid)
        {
            throw new ObjectDisposedException(nameof(handle));
        }
        return Marshal.ReadIntPtr(handle);
    }

    public SafeComObjectHandle Clone()
    {
        return FromIUnknown(handle);
    }

    public object ToObject()
    {
        return Marshal.GetObjectForIUnknown(handle);
    }

    public object ToObject<T>()
    {
        return (T)ToObject();
    }

    public static SafeComObjectHandle FromObject(object obj)
    {
        return new SafeComObjectHandle(Marshal.GetIUnknownForObject(obj));
    }

    public static SafeComObjectHandle FromIUnknown(IntPtr unk)
    {
        Marshal.AddRef(unk);
        return new SafeComObjectHandle(unk);
    }

    public static SafeComObjectHandle FromObject(object obj, Guid iid, bool throw_on_error = true)
    {
        using var ptr = FromObject(obj);
        return ptr.QueryInterface(iid, throw_on_error);
    }

    public static SafeComObjectHandle GetClassObject(Guid clsid, CLSCTX clsctx, Guid iid, COSERVERINFO server_info = null)
    {
        return NativeMethods.CoGetClassObject(clsid, clsctx, server_info, iid);
    }

    public static SafeComObjectHandle CreateInstance(Guid clsid, CLSCTX clsctx, Guid iid, object unk_outer = null)
    {
        return NativeMethods.CoCreateInstance(clsid, unk_outer, clsctx, iid);
    }
}
