﻿using SharpExtension.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QQS_UI.Core
{
    public abstract class RendererBase
    {
        protected readonly RenderFile renderFile;
        protected readonly UnmanagedList<Note>[] noteMap;
        protected readonly UnmanagedList<Tempo> tempos;

        protected readonly ushort ppq;
        protected readonly double noteSpeed;
        protected readonly int fps;
        protected readonly uint height;
        protected readonly uint keyHeight;
        protected readonly bool isTickBased;
        protected readonly bool isPreview;

        public bool Interrupt = false;
        public RendererBase(RenderFile file, in RenderOptions options)
        {
            renderFile = file;
            noteMap = file.Notes;
            tempos = file.Tempos;

            ppq = file.Division;
            noteSpeed = options.NoteSpeed;
            fps = options.FPS;
            height = (uint)options.Height;
            keyHeight = (uint)options.KeyHeight;
            isTickBased = options.TickBased;
            isPreview = options.PreviewMode;

            Console.WriteLine("Removing Overlaps...");
            _ = Parallel.For(0, 128, (i) =>
            {
                UnmanagedList<Note> nl = noteMap[i];
                if (nl.Count < 10)
                {
                    return;
                }
                for (long index = 0, len = nl.Count - 2; index != len;)
                {
                    ref Note curr = ref nl[index++];
                    ref Note next = ref nl[index];
                    if (curr.Start < next.Start && curr.End > next.Start && curr.End < next.End)
                    {
                        curr.End = next.Start;
                    }
                    else if (curr.Start == next.Start && curr.End <= next.End)
                    {
                        curr.End = curr.Start;
                    }
                }
            });
            Console.WriteLine("Overlap Remove process completed!");
        }

        public abstract void Render();
    }
}
