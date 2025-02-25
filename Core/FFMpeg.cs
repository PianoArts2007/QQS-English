﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SharpExtension.IO;
using Syste.IO;
using System.Windows;

namespace QQS_UI.Core
{
    /// <summary>
    /// 简单地封装操作ffmpeg的逻辑.
    /// </summary>
    public unsafe class FFMpeg : IDisposable
    {
        private readonly CStream stream;
        private readonly ulong frameSize;
        /// <summary>
        /// 初始化一个新的 <see cref="FFMpeg"/> 实例.
        /// </summary>
        /// <param name="ffargs">初始化ffmpeg的参数.</param>
        /// <param name="width">输入视频的宽.</param>
        /// <param name="height">输入视频的高.</param>
        public FFMpeg(string ffargs, int width, int height)
        {
            string ffcommand;
            stream = CStream.OpenPipe(ffcommand = "ffmpeg " + ffargs, "wb");
            frameSize = (uint)width * (uint)height * 4;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("FFMpeg Starting Command: {0}", ffcommand);

            string ffmpegPath = "ffmpeg.exe";
            if (File.Exists(ffmpegPath))
            {
                Console.WriteLine("ffmpeg.exe was found!");
            }
            else
            {
                _ = MessageBox.Show("ffmpeg.exe was not found \nMake sure that you downloaded and placed ffmpeg.exe in the program directory!", "Error");
                return;
            }
        }
        /// <summary>
        /// 向 FFMpeg 写入一帧.<br/>
        /// Write a frame to FFMpeg.
        /// </summary>
        /// <param name="buffer">存有视频画面的缓冲区.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFrame(in void* buffer)
        {
            _ = stream.WriteWithoutLock(buffer, frameSize, 1);
        }
        public void Dispose()
        {
            if (!stream.Closed)
            {
                _ = stream.Close();
            }
            GC.SuppressFinalize(this);
        }
        ~FFMpeg()
        {
            stream.Dispose();
        }
    }
}
