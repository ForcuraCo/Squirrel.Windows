#nullable enable

using System;
using System.ComponentModel;

namespace Squirrel
{
    internal class MsDeltaCompression
    {
        public void ApplyDelta(string deltaFilePath, string oldFilePath, string newFilePath)
        {
            if (!NativeMethods.ApplyDelta(ApplyFlags.AllowLegacy, oldFilePath, deltaFilePath, newFilePath))
                throw new Win32Exception();
        }
    }
}
