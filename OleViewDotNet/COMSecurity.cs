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

using NtApiDotNet;
using NtApiDotNet.Forms;
using NtApiDotNet.Win32;
using OleViewDotNet.Database;
using OleViewDotNet.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace OleViewDotNet;

[Flags]
public enum COMAccessRights : uint
{
    Execute = 1,
    ExecuteLocal = 2,
    ExecuteRemote = 4,
    ActivateLocal = 8,
    ActivateRemote = 16,
    ExecuteContainer = 32,
    ActivateContainer = 64,
    GenericRead = GenericAccessRights.GenericRead,
    GenericWrite = GenericAccessRights.GenericWrite,
    GenericExecute = GenericAccessRights.GenericExecute,
    GenericAll = GenericAccessRights.GenericAll,
}

public static class COMSecurity
{
    public static void ViewSecurity(COMRegistry registry, string name, string sddl, bool access)
    {
        if (!string.IsNullOrWhiteSpace(sddl))
        {
            SecurityDescriptor sd = new(sddl);
            AccessMask valid_access = access ? 0x7 : 0x1F;

            SecurityDescriptorViewerControl control = new();
            EntryPoint.GetMainForm(registry).HostControl(control, name);
            control.SetSecurityDescriptor(sd, typeof(COMAccessRights), new GenericMapping()
                { GenericExecute = valid_access, GenericRead = valid_access,
                GenericWrite = valid_access, GenericAll = valid_access }, valid_access);
        }
    }

    public static void ViewSecurity(COMRegistry registry, COMAppIDEntry appid, bool access)
    {
        ViewSecurity(registry, string.Format("{0} {1}", appid.Name, access ? "Access" : "Launch"),
                access ? appid.AccessPermission : appid.LaunchPermission, access);
    }

    internal static string GetSaclForSddl(string sddl)
    {
        return GetStringSDForSD(GetSDForStringSD(sddl), SecurityInformation.Label);
    }

    public static bool IsAccessGranted(string sddl, string principal, NtToken token, bool launch, bool check_il, COMAccessRights desired_access)
    {
        try
        {
            if (check_il)
            {
                string sacl = GetSaclForSddl(sddl);
                if (string.IsNullOrEmpty(sacl))
                {
                    // Add medium NX SACL
                    sddl += "S:(ML;;NX;;;ME)";
                }
            }

            if (!GetGrantedAccess(sddl, principal, token, launch, out COMAccessRights maximum_rights))
            {
                return false;
            }

            // If old style SD then all accesses are granted.
            if (maximum_rights == COMAccessRights.Execute)
            {
                return true;
            }

            return (maximum_rights & desired_access) == desired_access;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static bool GetGrantedAccess(string sddl, string principal, NtToken token, bool launch, out COMAccessRights maximum_rights)
    {
        GenericMapping mapping = new()
        {
            GenericExecute = (uint)(COMAccessRights.Execute | COMAccessRights.ExecuteLocal | COMAccessRights.ExecuteRemote | COMAccessRights.ExecuteContainer)
        };
        if (launch)
        {
            mapping.GenericExecute |= (uint)(COMAccessRights.ActivateLocal | COMAccessRights.ActivateRemote | COMAccessRights.ActivateContainer);
        }

        // If SD is only a NULL DACL we get maximum rights.
        if (sddl == "D:NO_ACCESS_CONTROL")
        {
            maximum_rights = mapping.GenericExecute.ToSpecificAccess<COMAccessRights>();
            return true;
        }

        AccessMask mask;

        if (!string.IsNullOrWhiteSpace(principal))
        {
            mask = NtSecurity.GetMaximumAccess(new SecurityDescriptor(sddl), token, new Sid(principal), mapping);
        }
        else
        {
            mask = NtSecurity.GetMaximumAccess(new SecurityDescriptor(sddl), token, mapping);
        }

        mask &= 0xFFFF;

        maximum_rights = mask.ToSpecificAccess<COMAccessRights>();

        return mask != 0;
    }

    private static string GetSecurityPermissions(COMSD sdtype)
    {
        IntPtr sd = IntPtr.Zero;
        try
        {
            int hr = NativeMethods.CoGetSystemSecurityPermissions(sdtype, out sd);
            if (hr != 0)
            {
                throw new Win32Exception(hr);
            }

            return new SecurityDescriptor(sd).ToSddl(SecurityInformation.AllBasic);}
        finally
        {
            if (sd != IntPtr.Zero)
            {
                NativeMethods.LocalFree(sd);
            }
        }
    }

    public static string GetDefaultLaunchPermissions()
    {
        return GetSecurityPermissions(COMSD.SD_LAUNCHPERMISSIONS);
    }

    public static string GetDefaultAccessPermissions()
    {
        return GetSecurityPermissions(COMSD.SD_ACCESSPERMISSIONS);
    }

    public static string GetDefaultLaunchRestrictions()
    {
        return GetSecurityPermissions(COMSD.SD_LAUNCHRESTRICTIONS);
    }

    public static string GetDefaultAccessRestrictions()
    {
        return GetSecurityPermissions(COMSD.SD_ACCESSRESTRICTIONS);
    }

    public static string GetStringSDForSD(byte[] sd, SecurityInformation info)
    {
        try
        {
            if (sd == null || sd.Length == 0)
            {
                return string.Empty;
            }

            return new SecurityDescriptor(sd).ToSddl(info);
        }
        catch (NtException)
        {
            return string.Empty;
        }
    }

    public static string GetStringSDForSD(byte[] sd)
    {
        return GetStringSDForSD(sd, SecurityInformation.AllBasic);
    }

    public static byte[] GetSDForStringSD(string sddl)
    {
        try
        {
            return new SecurityDescriptor(sddl).ToByteArray();
        }
        catch (NtException)
        {
            return new byte[0];
        }
    }

    public static TokenIntegrityLevel GetILForSD(string sddl)
    {
        if (string.IsNullOrWhiteSpace(sddl))
        {
            return TokenIntegrityLevel.Medium;
        }

        try
        {
            SecurityDescriptor sd = new(sddl);
            return sd.IntegrityLevel;
        }
        catch (NtException)
        {
            return TokenIntegrityLevel.Medium;
        }
    }

    private static bool SDHasAllowedAce(string sddl, bool allow_null_dacl, Func<Ace, bool> check_func)
    {
        if (string.IsNullOrWhiteSpace(sddl))
        {
            return allow_null_dacl;
        }

        try
        {
            SecurityDescriptor sd = new(sddl);
            if (allow_null_dacl && sd.Dacl == null && sd.Dacl.NullAcl)
            {
                return true;
            }
            foreach (var ace in sd.Dacl)
            {
                if (ace.Type == AceType.Allowed)
                {
                    if (check_func(ace))
                    {
                        return true;
                    }
                }
            }
        }
        catch (NtException)
        {
        }
        return false;
    }

    public static bool SDHasAC(string sddl)
    {
        SidIdentifierAuthority authority = new(SecurityAuthority.Package);
        return SDHasAllowedAce(sddl, false, ace => ace.Sid.Authority.Equals(authority));
    }

    public static bool SDHasRemoteAccess(string sddl)
    {
        return SDHasAllowedAce(sddl, true, a => a.Mask == COMAccessRights.Execute || 
            (a.Mask & (COMAccessRights.ExecuteRemote | COMAccessRights.ActivateRemote)) != 0);
    }

    public static IEnumerable<int> GetSessionIds()
    {
        return Win32Utils.GetConsoleSessions().Select(c => c.SessionId).ToArray();
    }

    public static Sid UserToSid(string username)
    {
        try
        {
            return NtSecurity.LookupAccountName(username);
        }
        catch (NtException)
        {
            return new Sid(username);
        }
    }
}
