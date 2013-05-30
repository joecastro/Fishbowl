//---------------------------------------------------------------------
// <autogenerated>
//
//     Generated by Message Compiler (mc.exe)
//
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </autogenerated>
//---------------------------------------------------------------------




namespace Standard
{
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;

    public static class ETWLogger
    {
        //
        // Provider Fishbowl Event Count 1
        //

        private static EventProviderVersionTwo m_provider = new EventProviderVersionTwo(new Guid("edda1d18-b527-4ce3-9918-b32498ded795"));
        //
        // Task :  eventGUIDs
        //
        private static Guid UnhandledDispatcherPoolExceptionId = new Guid("1f233c21-b2b8-4740-b2cf-ffdca06d0bf2");

        //
        // Event Descriptors
        //
        private static EventDescriptor UnhandledDispatcherPoolExceptionEvent;


        static ETWLogger()
        {
            unchecked
            {
                UnhandledDispatcherPoolExceptionEvent = new EventDescriptor(0x0, 0x0, 0x0, 0x2, 0x0, 0x1, (long)0x0);
            }
        }


        //
        // Event method for UnhandledDispatcherPoolExceptionEvent
        //
        public static bool EventWriteUnhandledDispatcherPoolExceptionEvent(string Message, string StackTrace)
        {

            if (!m_provider.IsEnabled())
            {
                return true;
            }

            return m_provider.TemplateExceptionTemplate(ref UnhandledDispatcherPoolExceptionEvent, Message, StackTrace);
        }
    }

    internal class EventProviderVersionTwo : EventProvider
    {
         internal EventProviderVersionTwo(Guid id)
                : base(id)
         {}


        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct EventData
        {
            [FieldOffset(0)]
            internal UInt64 DataPointer;
            [FieldOffset(8)]
            internal uint Size;
            [FieldOffset(12)]
            internal int Reserved;
        }



        internal unsafe bool TemplateExceptionTemplate(
            ref EventDescriptor eventDescriptor,
            string Message,
            string StackTrace
            )
        {
            int argumentCount = 2;
            bool status = true;

            if (IsEnabled(eventDescriptor.Level, eventDescriptor.Keywords))
            {
                byte* userData = stackalloc byte[sizeof(EventData) * argumentCount];
                EventData* userDataPtr = (EventData*)userData;

                userDataPtr[0].Size = (uint)(Message.Length + 1)*sizeof(char);

                userDataPtr[1].Size = (uint)(StackTrace.Length + 1)*sizeof(char);

                fixed (char* a0 = Message, a1 = StackTrace)
                {
                    userDataPtr[0].DataPointer = (ulong)a0;
                    userDataPtr[1].DataPointer = (ulong)a1;
                    status = WriteEvent(ref eventDescriptor, argumentCount, (IntPtr)(userData));
                }
            }

            return status;

        }

    }

}
