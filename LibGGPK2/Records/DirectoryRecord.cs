﻿using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LibGGPK2.Records
{
    public class DirectoryRecord : RecordTreeNode
    {
        public struct DirectoryEntry
        {
            /// <summary>
            /// Murmur2 hash of lowercase entry name
            /// </summary>
            public uint EntryNameHash;
            /// <summary>
            /// Offset in pack file where the record begins
            /// </summary>
            public long Offset;

            public DirectoryEntry(uint entryNameHash, long offset)
            {
                EntryNameHash = entryNameHash;
                Offset = offset;
            }
        }

        public static readonly byte[] Tag = Encoding.ASCII.GetBytes("PDIR");
        public static readonly SortComp Comparer = new SortComp();

        /// <summary>
        /// Records this directory contains. Each entry is an offset in the pack file of the record.
        /// </summary>
        public DirectoryEntry[] Entries;
        /// <summary>
        /// Offset in pack file where entries list begins. This is only here because it makes rewriting the entries list easier.
        /// </summary>
        public long EntriesBegin;

        public DirectoryRecord(int length, GGPKContainer ggpk)
        {
            ggpkContainer = ggpk;
            RecordBegin = ggpk.fileStream.Position - 8;
            Length = length;
            Read();
        }

        private SortedSet<RecordTreeNode> _Children;
        public override DirectoryRecord Parent { get; internal set; }
        public override SortedSet<RecordTreeNode> Children
        {
            get
            {
                if (_Children == null)
                {
                    _Children = new SortedSet<RecordTreeNode>(Comparer);
                    foreach (var e in Entries)
                    {
                        var b = ggpkContainer.GetRecord(e.Offset) as RecordTreeNode;
                        b.Parent = this;
                        _Children.Add(b);
                    }
                }
                return _Children;
            }
        }

        protected override void Read()
        {
            var br = ggpkContainer.Reader;
            var nameLength = br.ReadInt32();
            var totalEntries = br.ReadInt32();

            Hash = br.ReadBytes(32);
            Name = Encoding.Unicode.GetString(br.ReadBytes(2 * (nameLength - 1)));
            br.BaseStream.Seek(2, SeekOrigin.Current); // Null terminator

            EntriesBegin = br.BaseStream.Position;
            Entries = new DirectoryEntry[totalEntries];
            for (var i = 0; i < totalEntries; i++)
                Entries[i] = new DirectoryEntry (br.ReadUInt32(), br.ReadInt64());
        }

        internal override void Write(BinaryWriter bw = null)
        {
            if (bw == null)
                bw = ggpkContainer.Writer;
            RecordBegin = bw.BaseStream.Position;
            bw.Write(Length);
            bw.Write(Tag);
            bw.Write(Name.Length + 1);
            bw.Write(Entries.Length);
            bw.Write(Hash);
            bw.Write(Encoding.Unicode.GetBytes(Name));
            bw.Write((short)0); // Null terminator
            foreach (var entry in Entries)
            {
                bw.Write(entry.EntryNameHash);
                bw.Write(entry.Offset);
            }
        }
    }

    public class SortComp : IComparer<RecordTreeNode>
    {
        [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string x, string y);
        public virtual int Compare(RecordTreeNode x, RecordTreeNode y)
        {
            var dx = x as DirectoryRecord;
            var dy = y as DirectoryRecord;
            if (dx != null)
                if (dy != null)
                    return StrCmpLogicalW(dx.Name, dy.Name);
                else
                    return -1;
            else
                if (dy != null)
                    return 1;
                else
                    return StrCmpLogicalW(((FileRecord)x).Name, ((FileRecord)y).Name);
        }
    }
}