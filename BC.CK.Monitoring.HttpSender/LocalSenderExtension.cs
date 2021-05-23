using System;
using System.Runtime.CompilerServices;

// ReSharper disable ExplicitCallerInfoArgument
// ReSharper disable CheckNamespace
namespace CK.Core
{
    static class LocalSenderExtension
    {
        public static void SendLine(
            this IActivityMonitor @this, LogLevel level, string text,
            Exception? ex = null,
            CKTrait? tags = null,
            [CallerFilePath] string fileName = null!,
            [CallerLineNumber] int lineNumber = 0
        )
        {
            if (@this.ShouldLogLine(level, fileName, lineNumber))
            {
                @this.UnfilteredLog(tags, level | LogLevel.IsFiltered, text, @this.NextLogTime(), ex, fileName,
                    lineNumber);
            }
        }

        public static IDisposable OpenGroup(
            this IActivityMonitor @this, LogLevel level, string text,
            Exception? ex = null,
            CKTrait? tags = null,
            [CallerFilePath] string fileName = null!,
            [CallerLineNumber] int lineNumber = 0
        )
        {
            if (@this.ShouldLogGroup(level, fileName, lineNumber))
            {
                return @this.UnfilteredOpenGroup(new ActivityMonitorGroupData(level | LogLevel.IsFiltered, tags, text,
                    @this.NextLogTime(), ex, null, fileName, lineNumber));
            }

            return @this.UnfilteredOpenGroup(new ActivityMonitorGroupData());
        }
    }
}