﻿//    This file is part of OleViewDotNet.
//    Copyright (C) James Forshaw 2024
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

using OleViewDotNet.Interop;
using OleViewDotNet.Utilities;
using System;
using System.Runtime.InteropServices;

namespace OleViewDotNet.Rpc.Transport;

internal sealed class RpcChannelBufferProxy : RpcChannelBuffer
{
    // This is a manual wrapper for the IRpcChannelBuffer interface as the 
    // COM implementation seems to have a broken IMarshal interface.

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int fGetBuffer(SafeComObjectHandle This, ref RPC_MESSAGE pMessage, in Guid riid);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int fSendReceive(SafeComObjectHandle This, ref RPC_MESSAGE pMessage, out int pStatus);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int fFreeBuffer(SafeComObjectHandle This, ref RPC_MESSAGE pMessage);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int fGetDestCtx(SafeComObjectHandle This, out MSHCTX pdwDestContext, out IntPtr ppvDestContext);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int fIsConnected(SafeComObjectHandle This);

    private readonly SafeComObjectHandle m_proxy;
    private readonly SafeComObjectHandle m_buffer;
    private readonly Guid m_iid;
    private readonly fGetBuffer m_get_buffer;
    private readonly fSendReceive m_send_recv;
    private readonly fFreeBuffer m_free_buffer;
    private readonly fGetDestCtx m_get_dest_ctx;
    private readonly fIsConnected m_is_connected;

    private T GetFunc<T>(int index)
    {
        IntPtr vtable = m_buffer.ReadVTable();
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable + (IntPtr.Size * (index + 3))));
    }

    public RpcChannelBufferProxy(IntPtr buffer, Guid iid, SafeComObjectHandle proxy)
    {
        m_buffer = SafeComObjectHandle.FromIUnknown(buffer);
        m_iid = iid;
        m_proxy = proxy.Clone();
        m_get_buffer = GetFunc<fGetBuffer>(0);
        m_send_recv = GetFunc<fSendReceive>(1);
        m_free_buffer = GetFunc<fFreeBuffer>(2);
        m_get_dest_ctx = GetFunc<fGetDestCtx>(3);
        m_is_connected = GetFunc<fIsConnected>(4);
    }

    protected override void OnDispose()
    {
        m_buffer?.Dispose();
        m_proxy?.Dispose();
    }

    public override byte[] SendReceive(byte[] ndr_data, int proc_num)
    {
        RPC_MESSAGE msg = new()
        {
            BufferLength = ndr_data.Length,
            ProcNum = proc_num,
            DataRepresentation = 0x10,
        };
        m_get_buffer(m_buffer, ref msg, in m_iid).CheckHr();
        Marshal.Copy(ndr_data, 0, msg.Buffer, ndr_data.Length);
        m_send_recv(m_buffer, ref msg, out int status).CheckHr();
        status.CheckHr();
        byte[] ret = new byte[msg.BufferLength];
        Marshal.Copy(msg.Buffer, ret, 0, msg.BufferLength);
        m_free_buffer(m_buffer, ref msg).CheckHr();
        return ret;
    }

    public override bool IsConnected => m_is_connected(m_buffer) == 0;

    public override MSHCTX DestContext
    {
        get
        {
            m_get_dest_ctx(m_buffer, out MSHCTX dest_context, out IntPtr _).CheckHr();
            return dest_context;
        }
    }
}