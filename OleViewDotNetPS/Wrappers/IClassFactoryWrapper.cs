﻿//    This file is part of OleViewDotNet.
//    Copyright (C) James Forshaw 2018
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
using OleViewDotNet.Database;
using OleViewDotNet.Interop;
using OleViewDotNet.TypeManager;

namespace OleViewDotNetPS.Wrappers;

public sealed class IClassFactoryWrapper : BaseComWrapper<IClassFactory>
{
    public IClassFactoryWrapper(object obj, COMRegistry registry) : base(obj, registry)
    {
    }

    public void CreateInstance(object pUnkOuter, Guid riid, out ICOMObjectWrapper ppvObject)
    {
        _object.CreateInstance(pUnkOuter, riid, out object obj);
        ppvObject = COMTypeManager.Wrap(obj, riid, m_registry);
    }

    public ICOMObjectWrapper CreateInstance(object pUnkOuter, Guid riid)
    {
        CreateInstance(pUnkOuter, riid, out ICOMObjectWrapper obj);
        return obj;
    }

    public void LockServer(bool fLock)
    {
        _object.LockServer(fLock);
    }
}
