using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NO404.Core
{
    public enum LogLevel { Trace = 0, Info = 1, Warning = 2, Error = 3, Fatal = 4 }

    public readonly struct LogEntry
    {
        public readonly LogLevel Level;
        public readonly string Channel;
        public readonly string Message;
        public readonly float RealTime;

        public LogEntry(LogLevel level, string channel, string message, float realTime)
        {
            Level = level; Channel = channel; Message = message; RealTime = realTime;
        }

        public override string ToString()
        {
            return "[" + RealTime.ToString("0.00") + "][" + Level + "][" + Channel + "] " + Message;
        }
    }

    /// <summary>
    /// Logging front-end (GDD 20.21). Trace only survives in the editor. Every entry is also
    /// kept in a ring buffer so the dev console and QA log export can read it back.
    /// </summary>
    public static class Log
    {
        public const int BufferSize = 512;

        static readonly Queue<LogEntry> Buffer = new Queue<LogEntry>(BufferSize);
        public static LogLevel MinimumLevel = LogLevel.Info;
        public static event Action<LogEntry> OnEntry;

        /// <summary>
        /// Most recent non-trace line, pre-formatted. The facility OS log strip reads this
        /// every frame, so it must not allocate.
        /// </summary>
        public static string LastLine { get; private set; } = string.Empty;

        public static void Trace(string channel, string message)
        {
#if UNITY_EDITOR
            Write(LogLevel.Trace, channel, message);
#endif
        }

        public static void Info(string channel, string message) { Write(LogLevel.Info, channel, message); }
        public static void Warn(string channel, string message) { Write(LogLevel.Warning, channel, message); }
        public static void Error(string channel, string message) { Write(LogLevel.Error, channel, message); }
        public static void Fatal(string channel, string message) { Write(LogLevel.Fatal, channel, message); }

        static void Write(LogLevel level, string channel, string message)
        {
            if (level < MinimumLevel) return;

            var entry = new LogEntry(level, channel, message, Time.realtimeSinceStartup);
            if (Buffer.Count >= BufferSize) Buffer.Dequeue();
            Buffer.Enqueue(entry);

            if (level != LogLevel.Trace) LastLine = channel + " — " + message;

            switch (level)
            {
                case LogLevel.Warning: Debug.LogWarning(entry.ToString()); break;
                case LogLevel.Error:
                case LogLevel.Fatal: Debug.LogError(entry.ToString()); break;
                default: Debug.Log(entry.ToString()); break;
            }

            var cb = OnEntry;
            if (cb != null)
            {
                try { cb(entry); } catch (Exception) { /* never let a log sink break the game */ }
            }
        }

        public static LogEntry[] Snapshot() { return Buffer.ToArray(); }

        public static string Dump()
        {
            var sb = new StringBuilder();
            foreach (var e in Buffer) sb.AppendLine(e.ToString());
            return sb.ToString();
        }

        public static void ClearBuffer() { Buffer.Clear(); }
    }
}
