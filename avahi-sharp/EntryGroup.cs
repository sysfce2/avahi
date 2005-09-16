/* $Id$ */

/***
  This file is part of avahi.

  avahi is free software; you can redistribute it and/or modify it
  under the terms of the GNU Lesser General Public License as
  published by the Free Software Foundation; either version 2.1 of the
  License, or (at your option) any later version.

  avahi is distributed in the hope that it will be useful, but WITHOUT
  ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
  or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General
  Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with avahi; if not, write to the Free Software
  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
  USA.
***/

using System;
using System.Runtime.InteropServices;


namespace Avahi
{

    public enum EntryGroupState {
        Uncommited,
        Registering,
        Established,
        Collision
    }

    internal delegate void EntryGroupCallback (IntPtr group, EntryGroupState state, IntPtr userdata);
    public delegate void EntryGroupStateHandler (object o, EntryGroupState state);
    
    public class EntryGroup : IDisposable
    {
        private Client client;
        private IntPtr handle;
        private EntryGroupCallback cb;
        
        [DllImport ("avahi-client")]
        private static extern IntPtr avahi_entry_group_new (IntPtr client, EntryGroupCallback cb, IntPtr userdata);

        [DllImport ("avahi-client")]
        private static extern void avahi_entry_group_commit (IntPtr group);

        [DllImport ("avahi-client")]
        private static extern void avahi_entry_group_reset (IntPtr group);

        [DllImport ("avahi-client")]
        private static extern EntryGroupState avahi_entry_group_get_state (IntPtr group);

        [DllImport ("avahi-client")]
        private static extern bool avahi_entry_group_is_empty (IntPtr group);

        [DllImport ("avahi-client")]
        private static extern void avahi_entry_group_add_service_strlst (IntPtr group, int iface, Protocol proto,
                                                                         IntPtr name, IntPtr type, IntPtr domain,
                                                                         IntPtr host, UInt16 port, IntPtr strlst);
        
        [DllImport ("avahi-client")]
        private static extern void avahi_entry_group_free (IntPtr group);

        [DllImport ("avahi-common")]
        private static extern IntPtr avahi_string_list_new (IntPtr txt);

        [DllImport ("avahi-common")]
        private static extern IntPtr avahi_string_list_add (IntPtr list, IntPtr txt);

        [DllImport ("avahi-common")]
        private static extern void avahi_string_list_free (IntPtr list);

        [DllImport ("avahi-common")]
        private static extern IntPtr avahi_alternative_service_name (IntPtr name);

        public event EntryGroupStateHandler StateChanged;
        
        public EntryGroupState State
        {
            get {
                lock (client) {
                    return avahi_entry_group_get_state (handle);
                }
            }
        }

        public bool IsEmpty
        {
            get {
                lock (client) {
                    return avahi_entry_group_is_empty (handle);
                }
            }
        }
        
        public EntryGroup (Client client)
        {
            this.client = client;
            cb = OnEntryGroupCallback;

            lock (client) {
                handle = avahi_entry_group_new (client.Handle, cb, IntPtr.Zero);
                client.CheckError ();
            }
        }

        ~EntryGroup ()
        {
            Dispose ();
        }

        public void Dispose ()
        {
            if (client.Handle != IntPtr.Zero && handle != IntPtr.Zero) {
                lock (client) {
                    avahi_entry_group_free (handle);
                    handle = IntPtr.Zero;
                }
            }
        }

        public void Commit ()
        {
            lock (client) {
                avahi_entry_group_commit (handle);
                client.CheckError ();
            }
        }

        public void Reset ()
        {
            lock (client) {
                avahi_entry_group_reset (handle);
                client.CheckError ();
            }
        }

        public void AddService (string name, string type, string domain,
                                UInt16 port, params string[] txt)
        {
            AddService (-1, Protocol.Unspecified, name, type, domain, null, port, txt);
        }

        public void AddService (int iface, Protocol proto, string name, string type, string domain,
                                string host, UInt16 port, params string[] txt)
        {
            IntPtr list = avahi_string_list_new (IntPtr.Zero);

            if (txt != null) {
                foreach (string item in txt) {
                    IntPtr itemPtr = Utility.StringToPtr (item);
                    list = avahi_string_list_add (list, itemPtr);
                    Utility.Free (itemPtr);
                }
            }

            IntPtr namePtr = Utility.StringToPtr (name);
            IntPtr typePtr = Utility.StringToPtr (type);
            IntPtr domainPtr = Utility.StringToPtr (domain);
            IntPtr hostPtr = Utility.StringToPtr (host);

            lock (client) {
                avahi_entry_group_add_service_strlst (handle, iface, proto, namePtr, typePtr, domainPtr,
                                                      hostPtr, port, list);
            }
            
            avahi_string_list_free (list);

            client.CheckError ();
        }

        public static string GetAlternativeServiceName (string name) {
            IntPtr namePtr = Utility.StringToPtr (name);
            IntPtr result = avahi_alternative_service_name (namePtr);
            Utility.Free (namePtr);

            return Utility.PtrToStringFree (result);
        }

        private void OnEntryGroupCallback (IntPtr group, EntryGroupState state, IntPtr userdata)
        {
            if (StateChanged != null)
                StateChanged (this, state);
        }
    }
}