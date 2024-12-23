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

using OleViewDotNet.Database;
using OleViewDotNet.Interop;
using OleViewDotNet.Processes;
using OleViewDotNet.Proxy;
using OleViewDotNet.TypeLib.Instance;
using OleViewDotNet.Utilities;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace OleViewDotNet.TypeManager;

public static class COMTypeManager
{
    #region Private Members
    private static readonly Lazy<bool> m_loadtypes = new(LoadTypes);
    private static readonly ConcurrentDictionary<Tuple<Guid, short, short>, Assembly> m_typelibs = new();
    private static readonly ConcurrentDictionary<string, Assembly> m_typelibsname = new();
    private static readonly ConcurrentDictionary<Guid, Type> m_iidtypes = new();
    private static ICOMObjectWrapperScriptingFactory m_factory;
    private const string AUTOLOAD_PREFIX = "autoload_";

    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        if (m_typelibsname.TryGetValue(args.Name, out Assembly asm))
        {
            return asm;
        }
        return null;
    }

    private static string GetTypeLibDirectory()
    {
        string path = ProgramSettings.GetTypeLibDirectory();
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    private static bool ScriptingEnabled => m_factory is not null;

    internal static Tuple<Guid, short, short> GetLibKey(this COMTypeLibInstance type_lib)
    {
        var attrs = type_lib.LibAttr;
        return Tuple.Create(attrs.guid, attrs.wMajorVerNum, attrs.wMinorVerNum);
    }

    internal static string GetTypeLibAssemblyName(this COMTypeLibInstance type_lib)
    {
        try
        {
            return type_lib.GetCustData(COMKnownGuids.GUID_ExportedFromComPlus) as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool LoadTypes()
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        LoadTypes(Assembly.GetExecutingAssembly());
        LoadTypes(typeof(int).Assembly);
        foreach (var path in Directory.GetFiles(GetTypeLibDirectory(), $"{AUTOLOAD_PREFIX}*.dll"))
        {
            try
            {
                LoadTypes(Assembly.LoadFrom(path));
            }
            catch
            {
            }
        }
        return true;
    }

    private static void LoadTypes(Assembly asm)
    {
        foreach (Type t in asm.GetTypes().Where(x => x.IsPublic && x.IsInterface && IsComImport(x)))
        {
            if (t.GetCustomAttribute<ObsoleteAttribute>() is not null)
            {
                continue;
            }
            m_iidtypes.TryAdd(t.GUID, t);
        }
    }

    private static ICOMObjectWrapper Wrap(object obj, Guid iid, Type type, COMRegistry registry)
    {
        if (obj is ICOMObjectWrapper obj_wrapper)
        {
            obj = obj_wrapper.Unwrap();
        }

        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        if (!Marshal.IsComObject(obj))
        {
            throw new ArgumentException("Object must be a COM object.", nameof(obj));
        }

        if (!COMUtilities.SupportsInterface(obj, iid))
        {
            throw new ArgumentException($"Object doesn't support interface {iid}.", nameof(iid));
        }

        if (type is null)
        {
            return new COMObjectWrapper(obj, iid, null, null);
        }
        type = m_factory?.CreateType(type, iid) ?? type;
        try
        {
            if (typeof(ICOMObjectWrapper).IsAssignableFrom(type))
            {
                return (ICOMObjectWrapper)Activator.CreateInstance(type, obj, registry);
            }
        }
        catch
        {
        }
        return new COMObjectWrapper(obj, iid, type, registry);
    }
    #endregion

    #region Internal Members
    internal static Assembly ConvertTypeLibToAssembly(COMTypeLibInstance type_lib, IProgress<Tuple<string, int>> progress)
    {
        if (!m_loadtypes.Value)
        {
            throw new InvalidOperationException("Couldn't initialize types.");
        }

        var key = type_lib.GetLibKey();
        if (m_typelibs.ContainsKey(key))
        {
            return m_typelibs[key];
        }
        else
        {
            Assembly a;
            string name = type_lib.GetTypeLibAssemblyName();
            if (name is not null)
            {
                a = Assembly.Load(name);
            }
            else
            {
                string path = Path.Combine(GetTypeLibDirectory(), $"{key.Item1}_{key.Item2}_{key.Item3}.dll");
                if (!File.Exists(path))
                {
                    TypeLibConverter conv = new();
                    AssemblyBuilder asm = conv.ConvertTypeLibToAssembly(type_lib.Instance, path, TypeLibImporterFlags.None,
                                            new TypeLibCallback(progress), null, null, null, null);
                    asm.Save(Path.GetFileName(path));
                    a = asm;
                }
                else
                {
                    a = Assembly.LoadFrom(path);
                }
            }

            m_typelibs[key] = a;
            lock (m_typelibsname)
            {
                m_typelibsname[a.FullName] = a;
            }

            progress?.Report(Tuple.Create("Caching type information.", -1));
            LoadTypes(a);

            return a;
        }
    }
    #endregion

    #region Public Static Methods
    public static bool IsComImport(Type t)
    {
        return t.GetCustomAttributes(typeof(ComImportAttribute), false).Length > 0 ||
            t.GetCustomAttributes(typeof(InterfaceTypeAttribute), false).Length > 0;
    }

    public static bool IsComInterfaceType(Type t)
    {
        return IsComImport(t) && t.IsInterface && !t.Assembly.ReflectionOnly;
    }

    public static bool HasInterfaceType(Guid iid)
    {
        return GetInterfaceType(iid) is not null;
    }

    public static Type GetInterfaceType(Guid iid, COMRegistry registry)
    {
        if (registry is not null && registry.Interfaces.ContainsKey(iid))
        {
            return GetInterfaceType(registry.Interfaces[iid]);
        }

        return GetInterfaceType(iid);
    }

    public static Type GetInterfaceType(Guid iid)
    {
        if (m_loadtypes.Value && m_iidtypes.ContainsKey(iid))
        {
            return m_iidtypes[iid];
        }

        return null;
    }

    public static Type GetInterfaceType(COMInterfaceEntry intf)
    {
        if (intf is null)
        {
            return null;
        }

        Type type = GetInterfaceType(intf.Iid);
        if (type is not null)
        {
            return type;
        }

        if (intf.HasTypeLib)
        {
            try
            {
                using var type_lib = COMTypeLibInstance.FromFile(intf.TypeLibVersionEntry.NativePath);
                using var type_info = type_lib.GetTypeInfoOfGuid(intf.Iid);
                return m_iidtypes.GetOrAdd(intf.Iid, _ => type_info.ToType());
            }
            catch
            {
            }
        }

        if (intf.HasRuntimeType && intf.TryGetRuntimeType(out Type runtime_type))
        {
            return runtime_type;
        }

        if (intf.ProxyClassEntry is null)
        {
            return null;
        }

        var proxy = COMProxyInterface.GetFromIID(intf, intf.HasTypeLib);
        return m_iidtypes.GetOrAdd(intf.Iid, _ => proxy.CreateClientType(ScriptingEnabled));
    }

    public static Type GetInterfaceType(COMIPIDEntry ipid)
    {
        if (ipid is null)
        {
            return null;
        }

        Type type = GetInterfaceType(ipid.Iid);
        if (type is not null)
        {
            return type;
        }

        COMProxyFile proxy = ipid.ToProxyInstance();
        if (proxy is null)
        {
            return null;
        }

        m_iidtypes.TryAdd(ipid.Iid, proxy.Entries.Where(e => e.Iid == ipid.Iid).First().CreateClientType(ScriptingEnabled));
        return GetInterfaceType(ipid.Iid);
    }

    public static void LoadTypesFromAssembly(Assembly assembly, bool autoload = false)
    {
        LoadTypes(assembly);
        if (autoload)
        {
            string asm_path = assembly.Location;
            string path = Path.Combine(GetTypeLibDirectory(), $"{AUTOLOAD_PREFIX}{Guid.NewGuid()}.dll");
            File.Copy(asm_path, path);
        }
    }

    public static void LoadTypesFromAssembly(string path, bool autoload = false)
    {
        LoadTypesFromAssembly(Assembly.LoadFrom(path), autoload);
    }

    public static Assembly LoadTypeLib(string path, IProgress<Tuple<string, int>> progress)
    {
        using var type_lib = COMTypeLibInstance.FromFile(path);
        return ConvertTypeLibToAssembly(type_lib, progress);
    }

    public static Assembly LoadTypeLib(COMTypeLibVersionEntry type_lib, IProgress<Tuple<string, int>> progress)
    {
        return LoadTypeLib(type_lib.NativePath, progress);
    }


    public static Type GetDispatchTypeInfo(object obj, IProgress<Tuple<string, int>> progress)
    {
        if (!obj.GetType().IsCOMObject)
        {
            return obj.GetType();
        }
        else
        {
            if (obj is not IDispatch)
            {
                return null;
            }

            using var type_info = COMTypeInfoInstance.FromObject(obj);
            using var type_lib = type_info.GetContainingTypeLib();
            Guid iid = type_info.TypeAttr.guid;
            ConvertTypeLibToAssembly(type_lib, progress);
            return GetInterfaceType(iid);
        }
    }

    public static void SetScriptingFactory(ICOMObjectWrapperScriptingFactory factory)
    {
        m_factory = factory;
    }

    public static ICOMObjectWrapper Wrap(object obj, COMInterfaceEntry intf)
    {
        return Wrap(obj, intf.Iid, intf.Database);
    }

    public static ICOMObjectWrapper Wrap(object obj, COMInterfaceInstance intf)
    {
        return Wrap(obj, intf.Iid, intf.Database);
    }

    public static ICOMObjectWrapper Wrap(object obj, COMIPIDEntry ipid)
    {
        return Wrap(obj, ipid.Iid, ipid.Database);
    }

    public static ICOMObjectWrapper Wrap(object obj, Type intf_type, COMRegistry registry)
    {
        if (intf_type is null)
        {
            throw new ArgumentNullException(nameof(intf_type));
        }

        if (!IsComInterfaceType(intf_type))
        {
            throw new ArgumentException("Type must be a COM interface.");
        }
        return Wrap(obj, intf_type.GUID, intf_type, registry);
    }

    public static ICOMObjectWrapper Wrap(object obj, Guid iid, COMRegistry registry)
    {
        return Wrap(obj, iid, GetInterfaceType(iid, registry), registry);
    }

    public static object Unwrap(object obj)
    {
        if (obj is null)
        {
            return null;
        }

        if (Marshal.IsComObject(obj))
        {
            return obj;
        }

        if (obj is ICOMObjectWrapper wrapper)
        {
            return wrapper.Unwrap();
        }

        // If it's not a COM object or a wrapper then it's a managed object, just return as is.
        return obj;
    }
    #endregion

    #region Internal Members
    internal static void FlushIidType(Guid iid)
    {
        m_iidtypes?.TryRemove(iid, out _);
    }
    #endregion
}
